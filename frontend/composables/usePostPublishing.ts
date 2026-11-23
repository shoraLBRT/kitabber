// Writing a post through the site's server (ADR-0018), the same for the full editor (/write, E05-09) and the quick post
// above the feed (E05-29, ADR-0055): the topics to choose from, publishing (server/routes/bff/publish.post.ts) and
// uploading a photo to the media service (server/routes/bff/media.post.ts). A guest gets `unauthorized` without a
// request; the page opens the sign-in window for it (ADR-0032).
import type { PublishInput, PublishOutcome } from '~/api/publish'
import type { Topic } from '~/api/topics'
import type { MediaUpload } from '~/api/media'
import type { ImageUpload } from '~/editor/image'
import type { QuickPostHandoff } from '~/editor/quick-post'

export function usePostPublishing() {
  const account = useAccount()

  const publish = async (input: PublishInput): Promise<PublishOutcome> =>
    !account.value.signedIn
      ? { status: 'unauthorized' }
      : $fetch<PublishOutcome>('/bff/publish', { method: 'POST', body: input }).catch(
          (error: { statusCode?: number }): PublishOutcome =>
            error.statusCode === 401
              ? { status: 'unauthorized' }
              : { status: 'error', draftId: input.draftId },
        )

  const uploadImage = async (file: File): Promise<ImageUpload> => {
    if (!account.value.signedIn) return { status: 'unauthorized' }
    const form = new FormData()
    form.append('file', file, file.name)
    const outcome = await $fetch<MediaUpload>('/bff/media', { method: 'POST', body: form }).catch(
      (): MediaUpload => ({ status: 'error' }),
    )
    return outcome.status === 'uploaded' ? { status: 'uploaded', media: outcome.media.id } : outcome
  }

  /** A published post opens on its page; the feeds ask again when they open next, so the new post is there. */
  function openPublished(path: string, replace = false) {
    clearNuxtData((key) => key.startsWith('feed'))
    void navigateTo(path, { replace })
  }

  return { account, publish, uploadImage, openPublished }
}

/** The topics a post can go to; an unavailable list leaves «Без темы» only. Read once per page and shared. */
export function useTopics() {
  return useAsyncData('topics', () => $fetch<Topic[]>('/bff/topics').catch(() => [] as Topic[]), {
    lazy: true,
    default: () => [] as Topic[],
  })
}

/**
 * The quick post's state shared across the page (ADR-0055): whether the phone sheet of the quick post is open, and the
 * text and photos «Развернуть» hands to the full editor (taken once by /write).
 */
export function useQuickPost() {
  const sheetOpen = useState('quick-post:sheet', () => false)
  const handoff = useState<QuickPostHandoff | null>('quick-post:handoff', () => null)
  return { sheetOpen, handoff }
}
