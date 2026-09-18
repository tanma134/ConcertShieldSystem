-- Non-destructive Wishlist migration. No cross-database foreign key is added for user_id.
CREATE UNIQUE INDEX IF NOT EXISTS uq_wishlists_user_event
ON public.wishlists(user_id, event_id);
