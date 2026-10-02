export type Track = {
  title: string | null;
  artist: string | null;
  channel: string | null;
  durationSeconds: number | null;
  /** For a Spotify link: the YouTube video whose audio was analyzed, a best-guess match. */
  matchedUrl: string | null;
};
