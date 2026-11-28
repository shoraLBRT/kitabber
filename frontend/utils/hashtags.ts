// The hashtags of a post's text (E11-08, FR-TOPIC, ADR-0120): a `#` and a word of letters, digits and `_` with at least
// one letter, not glued to a word or an entity before it, at most 64 characters. The same rule as the server's
// (backend/src/Modules/Posts/Hashtags.cs), which takes the tags of a published post; here each tag of the text becomes a
// link to the feed of the tag. The case of a tag does not matter: the link is to its lower-case form.

/** The longest tag; a longer word after `#` is not a tag. */
export const hashtagMaxLength = 64

// `&amp;` before `#` is a `&` in the text: the server sees `&#…` there and takes no tag.
const tag = /(?<![\p{L}\p{M}\p{N}_&#]|&amp;)#([\p{L}\p{M}\p{N}_]{1,64})(?![\p{L}\p{M}\p{N}_])/gu
const letter = /\p{L}/u

/** The address of the feed of a tag. */
export function tagPath(name: string): string {
  return `/tags/${encodeURIComponent(name.toLowerCase())}`
}

/** The stored form of a tag from an address, or null when it is not a tag (`2026`, two words). */
export function parseTag(value: string): string | null {
  const word = value.trim().replace(/^#/, '')
  return word.length > 0 &&
    word.length <= hashtagMaxLength &&
    /^[\p{L}\p{M}\p{N}_]+$/u.test(word) &&
    letter.test(word)
    ? word.toLowerCase()
    : null
}

/**
 * Inline HTML already cleaned by cleanInlineHtml with every hashtag of its text a link to the tag's feed. The text
 * inside a link stays as it is: a link inside a link is not valid HTML.
 */
export function linkHashtags(html: string): string {
  let inLink = 0
  return html
    .split(/(<[^>]*>)/)
    .map((token, index) => {
      if (index % 2 === 1) {
        if (/^<a[\s>]/i.test(token)) inLink++
        else if (/^<\/a>/i.test(token)) inLink = Math.max(0, inLink - 1)
        return token
      }
      if (inLink > 0) return token
      return token.replace(tag, (whole, name: string) =>
        letter.test(name) ? `<a href="${tagPath(name)}" class="hashtag">${whole}</a>` : whole,
      )
    })
    .join('')
}
