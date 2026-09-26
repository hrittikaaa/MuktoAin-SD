-- scripts/21_review_timing_and_snapshot.sql
-- Lawyer review fixes:
--   * GENERATED_DOCUMENT.SubmittedForReviewAt: when the citizen last sent the
--     document to the review pool. The queue orders by it and shows the SLA
--     wait from it (CreatedAt is when the AI draft was generated, so old
--     drafts and resubmissions showed inflated waits). NULL for rows sent
--     before this column existed; the app falls back to CreatedAt.
--   * LAWYER_REVIEW.ReviewedVersionNo / ReviewedContent: the document version
--     and text as they stood when the decision was made, so a lawyer's
--     history doesn't change after a rejection and a citizen re-edit. NULL for
--     older reviews; the app falls back to the current document.
-- Idempotent: safe to re-run.
USE [MuktoAin];
GO
SET NOCOUNT ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[GENERATED_DOCUMENT]')
                 AND name = N'SubmittedForReviewAt')
BEGIN
    ALTER TABLE [dbo].[GENERATED_DOCUMENT] ADD [SubmittedForReviewAt] DATETIME2 NULL;
    PRINT 'GENERATED_DOCUMENT.SubmittedForReviewAt added.';
END
ELSE
    PRINT 'GENERATED_DOCUMENT.SubmittedForReviewAt already exists.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[LAWYER_REVIEW]')
                 AND name = N'ReviewedVersionNo')
BEGIN
    ALTER TABLE [dbo].[LAWYER_REVIEW] ADD [ReviewedVersionNo] INT NULL;
    PRINT 'LAWYER_REVIEW.ReviewedVersionNo added.';
END
ELSE
    PRINT 'LAWYER_REVIEW.ReviewedVersionNo already exists.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[LAWYER_REVIEW]')
                 AND name = N'ReviewedContent')
BEGIN
    ALTER TABLE [dbo].[LAWYER_REVIEW] ADD [ReviewedContent] NVARCHAR(MAX) NULL;
    PRINT 'LAWYER_REVIEW.ReviewedContent added.';
END
ELSE
    PRINT 'LAWYER_REVIEW.ReviewedContent already exists.';
GO
