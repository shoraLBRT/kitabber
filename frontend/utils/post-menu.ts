// The «…» menu of a post (E07-05, FR-POST, docs/ui/post.md, ADR-0053): what it holds depends on who reads. The author
// edits and deletes their post; a reader (a guest too) reports it, hides it and copies its link. There is no
// promotion, no donation and no gift (no monetization). A signed-in reader also blocks the author (E11-06). An item
// whose feature is not built yet stays hidden behind its flag (`runtimeConfig.public.features`, NUXT_PUBLIC_FEATURES_*)
// until its own task turns it on.
import { Ban, EyeOff, Flag, Link, Pencil, Trash2 } from 'lucide-vue-next'
import type { useToast } from '../composables/useToast'
import type { MenuItem } from '../types/ui'
import { t } from '../i18n'

/** The flags of the menu items that wait for their own tasks: editing (#146 and the editor), reporting (E15-02), hiding (E11-07). */
export interface PostMenuFeatures {
  postEdit: boolean
  postReport: boolean
  postHide: boolean
}

export const noPostMenuFeatures: PostMenuFeatures = { postEdit: false, postReport: false, postHide: false }

export type PostMenuAction = 'edit' | 'delete' | 'report' | 'hide' | 'copy-link' | 'block'

export interface PostMenuItem extends MenuItem {
  id: PostMenuAction
}

/**
 * The items of the menu: the author's when the reader wrote the post, a reader's otherwise; flagged items only when on.
 * «Заблокировать автора» (E11-06, ADR-0091) only when the reader may block (`canBlock`: signed in, the author known).
 */
export function postMenuItems(own: boolean, features: PostMenuFeatures, canBlock = false): PostMenuItem[] {
  const items: (PostMenuItem | false)[] = own
    ? [
        features.postEdit && { id: 'edit', label: t('actions.edit'), icon: Pencil },
        { id: 'delete', label: t('actions.delete'), icon: Trash2, danger: true },
      ]
    : [
        features.postReport && { id: 'report', label: t('actions.report'), icon: Flag },
        features.postHide && { id: 'hide', label: t('actions.hide'), icon: EyeOff },
        { id: 'copy-link', label: t('actions.copyLink'), icon: Link },
        canBlock && { id: 'block', label: t('blocks.blockAuthor'), icon: Ban, danger: true },
      ]
  return items.filter((item): item is PostMenuItem => Boolean(item))
}

/** Copies the full link of a post's address and says so in a toast: «Поделиться» of the panel and the menu's item. */
export async function copyPostLink(path: string, toast: ReturnType<typeof useToast>): Promise<void> {
  try {
    await navigator.clipboard.writeText(new URL(path, window.location.origin).href)
    toast.show(t('actions.linkCopied'), { tone: 'success' })
  } catch {
    toast.show(t('actions.copyLinkFailed'), { tone: 'error' })
  }
}
