// Picks the most useful message out of an axios error, with a fallback for network failures.
export function apiErrorMessage(error, fallback = "Something went wrong. Please try again.") {
  const data = error?.response?.data;
  if (typeof data === "string" && data.trim()) return data;
  return data?.message || data?.title || fallback;
}
