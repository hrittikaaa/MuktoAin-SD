using Microsoft.EntityFrameworkCore;
using MuktoAin.Domain.Entities;
using MuktoAin.Infrastructure.Data;
using MuktoAin.Infrastructure.Data.Seeding;
using MuktoAin.UnitTests.Repositories;
using Xunit;

namespace MuktoAin.UnitTests.Seeding;

// T-1.9 section chunking. Numbers below follow the service's constants:
// ~3 chars/token, split only above 500 tokens (> 1502 chars), 1200-char
// target windows with a 150-char overlap, and a boundary must land at least
// halfway (600 chars) into the window to be used instead of a hard cut.
public class LegalChunkingServiceTests
{
    private const int SplitThresholdChars = 1502; // 1502 / 3 = 500 tokens -> not split
    private const int TargetChars = 1200;
    private const int OverlapChars = 150;

    private static async Task<AppDbContext> ContextWithSectionsAsync(params string[] sectionTexts)
    {
        var context = TestDbContextFactory.Create();
        var act = new Act { ActId = 1, Title = "Test Act", ActNumber = "1", Year = 2000 };
        context.Acts.Add(act);
        var position = 1;
        foreach (var text in sectionTexts)
        {
            context.ActSections.Add(new ActSection { ActId = 1, OrdinalPosition = position++, SectionText = text });
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return context;
    }

    private static async Task<List<ActSectionChunk>> ChunksOfAsync(AppDbContext context, int sectionId) =>
        await context.ActSectionChunks
            .Where(c => c.SectionId == sectionId)
            .OrderBy(c => c.ChunkOrder)
            .ToListAsync();

    private static async Task<int> OnlySectionIdAsync(AppDbContext context) =>
        (await context.ActSections.SingleAsync()).SectionId;

    // Builds text of the given length with no spaces, periods or newlines,
    // so every split is a hard cut at the window edge.
    private static string Unbroken(int length) =>
        string.Concat(Enumerable.Range(0, length).Select(i => (char)('a' + i % 26)));

    [Fact]
    public async Task ChunkAsync_NoSections_CreatesNothing()
    {
        await using var context = TestDbContextFactory.Create();

        await LegalChunkingService.ChunkAsync(context);

        Assert.Empty(context.ActSectionChunks);
    }

    [Fact]
    public async Task ChunkAsync_ShortSection_BecomesSingleChunkWithSameText()
    {
        const string text = "1. This Act may be called the Labour Act, 2006.";
        await using var context = await ContextWithSectionsAsync(text);

        await LegalChunkingService.ChunkAsync(context);

        var chunk = Assert.Single(await context.ActSectionChunks.ToListAsync());
        Assert.Equal(text, chunk.ChunkText);
        Assert.Equal(1, chunk.ChunkOrder);
        Assert.Equal(text.Length / 3, chunk.TokenCount);
    }

    [Fact]
    public async Task ChunkAsync_NewChunks_HaveNoEmbeddingMetadataYet()
    {
        await using var context = await ContextWithSectionsAsync("Some section text.");

        await LegalChunkingService.ChunkAsync(context);

        var chunk = Assert.Single(await context.ActSectionChunks.ToListAsync());
        Assert.Null(chunk.VectorId);
        Assert.Null(chunk.ContentHash);
        Assert.Null(chunk.LastEmbeddedAt);
    }

    [Fact]
    public async Task ChunkAsync_TinySection_GetsMinimumTokenCountOfOne()
    {
        await using var context = await ContextWithSectionsAsync("ক");

        await LegalChunkingService.ChunkAsync(context);

        Assert.Equal(1, Assert.Single(await context.ActSectionChunks.ToListAsync()).TokenCount);
    }

    [Fact]
    public async Task ChunkAsync_SectionAtThreshold_IsNotSplit()
    {
        await using var context = await ContextWithSectionsAsync(Unbroken(SplitThresholdChars));

        await LegalChunkingService.ChunkAsync(context);

        Assert.Single(await context.ActSectionChunks.ToListAsync());
    }

    [Fact]
    public async Task ChunkAsync_SectionJustOverThreshold_IsSplit()
    {
        await using var context = await ContextWithSectionsAsync(Unbroken(SplitThresholdChars + 1));

        await LegalChunkingService.ChunkAsync(context);

        Assert.Equal(2, await context.ActSectionChunks.CountAsync());
    }

    [Fact]
    public async Task ChunkAsync_UnbrokenText_HardCutsWithFixedOverlap()
    {
        var text = Unbroken(3000);
        await using var context = await ContextWithSectionsAsync(text);

        await LegalChunkingService.ChunkAsync(context);

        var chunks = await ChunksOfAsync(context, await OnlySectionIdAsync(context));
        Assert.Equal(3, chunks.Count);
        Assert.Equal(text[..TargetChars], chunks[0].ChunkText);
        Assert.Equal(text.Substring(TargetChars - OverlapChars, TargetChars), chunks[1].ChunkText);
        Assert.Equal(text[(2 * (TargetChars - OverlapChars))..], chunks[2].ChunkText);
    }

    [Fact]
    public async Task ChunkAsync_SplitSection_ChunkOrderIsSequentialFromOne()
    {
        await using var context = await ContextWithSectionsAsync(Unbroken(6000));

        await LegalChunkingService.ChunkAsync(context);

        var chunks = await ChunksOfAsync(context, await OnlySectionIdAsync(context));
        Assert.Equal(Enumerable.Range(1, chunks.Count).Select(i => (short)i), chunks.Select(c => c.ChunkOrder));
    }

    [Fact]
    public async Task ChunkAsync_SplitSection_NoChunkExceedsTargetWindow()
    {
        var words = string.Join(" ", Enumerable.Range(0, 1500).Select(i => $"word{i}"));
        await using var context = await ContextWithSectionsAsync(words);

        await LegalChunkingService.ChunkAsync(context);

        var chunks = await ChunksOfAsync(context, await OnlySectionIdAsync(context));
        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.ChunkText.Length <= TargetChars));
    }

    [Fact]
    public async Task ChunkAsync_SplitSection_CoversWholeTextFromStartToEnd()
    {
        var text = string.Join(". ", Enumerable.Range(0, 400).Select(i => $"Sentence number {i}"));
        await using var context = await ContextWithSectionsAsync(text);

        await LegalChunkingService.ChunkAsync(context);

        var chunks = await ChunksOfAsync(context, await OnlySectionIdAsync(context));
        Assert.StartsWith(chunks[0].ChunkText, text);
        Assert.EndsWith(chunks[^1].ChunkText, text);
    }

    [Fact]
    public async Task ChunkAsync_ConsecutiveChunks_Overlap()
    {
        var text = string.Join(" ", Enumerable.Range(0, 1500).Select(i => $"w{i:D4}"));
        await using var context = await ContextWithSectionsAsync(text);

        await LegalChunkingService.ChunkAsync(context);

        var chunks = await ChunksOfAsync(context, await OnlySectionIdAsync(context));
        for (var i = 1; i < chunks.Count; i++)
        {
            // The next chunk begins inside the previous one, so the tail of the
            // previous chunk reappears at the head of the next.
            var head = chunks[i].ChunkText[..20];
            Assert.Contains(head, chunks[i - 1].ChunkText);
        }
    }

    [Fact]
    public async Task ChunkAsync_PrefersParagraphBreakOverSentenceAndWord()
    {
        // Paragraph break at 900, sentence break at 1100, spaces throughout:
        // the paragraph break wins because it is past the halfway floor.
        var text = Unbroken(898) + "\n\n" + Unbroken(198) + ". " + Unbroken(1000) + " tail words here";
        await using var context = await ContextWithSectionsAsync(text);

        await LegalChunkingService.ChunkAsync(context);

        var first = (await ChunksOfAsync(context, await OnlySectionIdAsync(context)))[0];
        Assert.Equal(900, first.ChunkText.Length);
        Assert.EndsWith("\n\n", first.ChunkText);
    }

    [Fact]
    public async Task ChunkAsync_PrefersSentenceBreakOverWordBreak()
    {
        // Sentence break ends at 1000, a lone space at 1100.
        var text = Unbroken(998) + ". " + Unbroken(99) + " " + Unbroken(1000);
        await using var context = await ContextWithSectionsAsync(text);

        await LegalChunkingService.ChunkAsync(context);

        var first = (await ChunksOfAsync(context, await OnlySectionIdAsync(context)))[0];
        Assert.Equal(1000, first.ChunkText.Length);
        Assert.EndsWith(". ", first.ChunkText);
    }

    [Fact]
    public async Task ChunkAsync_FallsBackToWordBreak_WhenNoParagraphOrSentence()
    {
        var text = Unbroken(1049) + " " + Unbroken(1000);
        await using var context = await ContextWithSectionsAsync(text);

        await LegalChunkingService.ChunkAsync(context);

        var first = (await ChunksOfAsync(context, await OnlySectionIdAsync(context)))[0];
        Assert.Equal(1050, first.ChunkText.Length);
        Assert.EndsWith(" ", first.ChunkText);
    }

    [Fact]
    public async Task ChunkAsync_IgnoresBoundaryBeforeHalfwayFloor_AndHardCuts()
    {
        // The only boundary sits at 300, under the 600-char floor, so the
        // first chunk is a hard cut at the full 1200-char window.
        var text = Unbroken(298) + "\n\n" + Unbroken(2000);
        await using var context = await ContextWithSectionsAsync(text);

        await LegalChunkingService.ChunkAsync(context);

        var first = (await ChunksOfAsync(context, await OnlySectionIdAsync(context)))[0];
        Assert.Equal(TargetChars, first.ChunkText.Length);
    }

    [Fact]
    public async Task ChunkAsync_TokenCountOfEachChunk_IsLengthOverThree()
    {
        await using var context = await ContextWithSectionsAsync(Unbroken(4000));

        await LegalChunkingService.ChunkAsync(context);

        Assert.All(await context.ActSectionChunks.ToListAsync(),
            c => Assert.Equal(Math.Max(1, c.ChunkText.Length / 3), c.TokenCount));
    }

    [Fact]
    public async Task ChunkAsync_EachSectionIsChunkedIndependently()
    {
        await using var context = await ContextWithSectionsAsync("Short one.", Unbroken(3000), "Short two.");

        await LegalChunkingService.ChunkAsync(context);

        var sections = await context.ActSections.OrderBy(s => s.OrdinalPosition).ToListAsync();
        Assert.Single(await ChunksOfAsync(context, sections[0].SectionId));
        Assert.Equal(3, (await ChunksOfAsync(context, sections[1].SectionId)).Count);
        Assert.Single(await ChunksOfAsync(context, sections[2].SectionId));
    }

    [Fact]
    public async Task ChunkAsync_MoreSectionsThanOneBatch_ChunksThemAll()
    {
        var texts = Enumerable.Range(1, 450).Select(i => $"Section {i} text.").ToArray();
        await using var context = await ContextWithSectionsAsync(texts);

        await LegalChunkingService.ChunkAsync(context);

        Assert.Equal(450, await context.ActSectionChunks.CountAsync());
        Assert.Equal(450, await context.ActSectionChunks.Select(c => c.SectionId).Distinct().CountAsync());
    }

    [Fact]
    public async Task ChunkAsync_ChunksAlreadyPresent_SkipsEntirely()
    {
        await using var context = await ContextWithSectionsAsync("First.", "Second.");
        var firstId = (await context.ActSections.OrderBy(s => s.OrdinalPosition).FirstAsync()).SectionId;
        context.ActSectionChunks.Add(new ActSectionChunk { SectionId = firstId, ChunkOrder = 1, ChunkText = "pre-seeded" });
        await context.SaveChangesAsync();

        await LegalChunkingService.ChunkAsync(context);

        // Fast path: any existing chunk means the corpus is treated as done,
        // so the second section is not chunked on this run.
        var chunk = Assert.Single(await context.ActSectionChunks.ToListAsync());
        Assert.Equal("pre-seeded", chunk.ChunkText);
    }

    [Fact]
    public async Task ChunkAsync_RunTwice_DoesNotDuplicateChunks()
    {
        await using var context = await ContextWithSectionsAsync("One.", Unbroken(3000));

        await LegalChunkingService.ChunkAsync(context);
        var afterFirst = await context.ActSectionChunks.CountAsync();
        await LegalChunkingService.ChunkAsync(context);

        Assert.Equal(afterFirst, await context.ActSectionChunks.CountAsync());
    }

    [Fact]
    public async Task ChunkAsync_LeavesChangeTrackerClearAfterRun()
    {
        await using var context = await ContextWithSectionsAsync("One.", "Two.");

        await LegalChunkingService.ChunkAsync(context);

        Assert.Empty(context.ChangeTracker.Entries());
    }
}
