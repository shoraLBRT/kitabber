// The pictures of reactions (E02-12, ADR-0095): Fluent Emoji Flat by Microsoft, MIT (brand/reactions/LICENSE), one
// SVG per code, kept in brand/ with the other graphics (ADR-0011) and bundled with the site. A reaction is drawn from
// its picture, not from the system's emoji font, so it looks the same on every system and browser. The codes are
// those of ReactionGraphics in the Reactions module.
import brand from '../../brand/brand.json'
import clap from '../../brand/reactions/clap.svg?url'
import heart from '../../brand/reactions/heart.svg?url'
import idea from '../../brand/reactions/idea.svg?url'
import like from '../../brand/reactions/like.svg?url'
import sad from '../../brand/reactions/sad.svg?url'
import smile from '../../brand/reactions/smile.svg?url'
import wow from '../../brand/reactions/wow.svg?url'

export const reactionGraphics: Readonly<Record<string, string>> = { like, heart, smile, clap, idea, wow, sad }

/** The address of the picture of `code`; `null` for a code this build has no picture for. */
export function reactionGraphic(code: string): string | null {
  return Object.hasOwn(reactionGraphics, code) ? reactionGraphics[code]! : null
}

/** Where the pictures come from, for the credit the licence asks for (brand.json `reactions`). */
export const reactionGraphicsSource = {
  name: brand.reactions.name,
  author: brand.reactions.author,
  license: brand.reactions.license,
  url: brand.reactions.source,
} as const
