// The client's side of the checks before publishing (E05-09, ADR-0046). The server checks everything again
// (Platform.Modules.Posts PostChecks); here only what the editor can tell at once: an empty post (isEmptyDocument)
// and the length of the title.

/** The longest title, in characters after trimming (PostChecks.TitleMaxLength). */
export const postTitleMaxLength = 120

/**
 * The title's length as the server counts it: UTF-16 code units of the trimmed text (.NET `string.Length`), so an
 * emoji counts two, as there.
 */
export function titleLength(title: string): number {
  return title.trim().length
}
