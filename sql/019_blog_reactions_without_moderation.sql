BEGIN;
-- Likes and share records take effect immediately. Comments and replies keep moderation.
UPDATE cricastudio.blog_activity
SET status='approved',moderated_at=NULL,moderated_by=NULL
WHERE kind IN ('like','share') AND (status<>'approved' OR moderated_at IS NOT NULL OR moderated_by IS NOT NULL);
DO $$ BEGIN
 IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='cricastudio.blog_activity'::regclass AND conname='blog_activity_comments_only_moderation') THEN
  ALTER TABLE cricastudio.blog_activity ADD CONSTRAINT blog_activity_comments_only_moderation CHECK(kind='comment' OR status='approved');
 END IF;
END $$;
COMMIT;
