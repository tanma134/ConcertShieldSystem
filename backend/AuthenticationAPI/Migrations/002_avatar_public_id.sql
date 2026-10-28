-- Non-destructive avatar metadata migration. Image binaries remain in Cloudinary.
ALTER TABLE public.users ADD COLUMN IF NOT EXISTS avatar_public_id text;
