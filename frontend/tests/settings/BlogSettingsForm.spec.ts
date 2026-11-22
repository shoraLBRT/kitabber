import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import BlogSettingsForm from '../../components/settings/BlogSettingsForm.vue'
import { useToast } from '../../composables/useToast'
import type { MediaUpload } from '../../api/media'
import type { Profile, ProfileInput, ProfileSave } from '../../api/profiles'

// The «Блог» section of the settings (E04-03, docs/ui/settings.md): the limits checked as the person types, the save
// with its outcomes, and the avatar that goes through the media service before the profile is saved. The crop itself
// needs a real canvas, so here the cropper is a stand-in that hands back a picture; tests/settings/avatar-crop.spec.ts
// checks the crop and e2e/settings.spec.ts the whole flow in a browser.
const anna: Profile = {
  id: '0195f3a0-0000-7000-8000-0000000000b1',
  type: 'personal',
  name: 'Анна',
  nickname: 'anna',
  description: null,
  registeredAt: '2026-03-14T10:00:00Z',
  avatarId: null,
  avatarUrl: null,
  avatarSources: [],
}

const cropped = new Blob(['jpeg'], { type: 'image/jpeg' })
const CropperStub = defineComponent({
  emits: ['done', 'cancel', 'failed'],
  setup:
    (_, { emit }) =>
    () =>
      h(
        'button',
        { type: 'button', 'data-testid': 'crop-apply', onClick: () => emit('done', cropped) },
        'Применить',
      ),
})

const toast = useToast()
afterEach(() => toast.clear())

function mountForm(
  save: (input: ProfileInput) => Promise<ProfileSave> = async (input) => ({
    status: 'saved',
    profile: { ...anna, ...input, avatarId: input.avatarId ?? null },
  }),
  upload: (avatar: Blob, fileName: string) => Promise<MediaUpload> = async (_, fileName) => ({
    status: 'uploaded',
    media: { id: 'media-1', kind: 'image', contentType: 'image/jpeg', size: 4, fileName, createdAt: '' },
  }),
  profile: Profile = anna,
) {
  const saveSpy = vi.fn(save)
  const uploadSpy = vi.fn(upload)
  const wrapper = mount(BlogSettingsForm, {
    props: { profile, save: saveSpy, upload: uploadSpy },
    attachTo: document.body,
    global: { stubs: { teleport: true, AvatarCropper: CropperStub } },
  })
  return { wrapper, saveSpy, uploadSpy }
}

const name = (wrapper: ReturnType<typeof mount>) => wrapper.get('input[data-field="name"]')
const description = (wrapper: ReturnType<typeof mount>) => wrapper.get('textarea[data-field="description"]')

async function chooseFile(wrapper: ReturnType<typeof mount>, file: File) {
  const input = wrapper.get('[data-testid="avatar-file"]')
  Object.defineProperty(input.element, 'files', { value: [file], configurable: true })
  await input.trigger('change')
  await flushPromises()
}

describe('BlogSettingsForm', () => {
  it('starts from the profile and counts characters as people see them', async () => {
    const { wrapper } = mountForm()
    expect((name(wrapper).element as HTMLInputElement).value).toBe('Анна')
    expect(wrapper.text()).toContain('4/25')
    expect(wrapper.text()).toContain('0/160')
    await description(wrapper).setValue('📚📚')
    expect(wrapper.text()).toContain('2/160')
    wrapper.unmount()
  })

  it('shows a limit error at once, keeps the request and focuses the field', async () => {
    const { wrapper, saveSpy } = mountForm()
    await name(wrapper).setValue('a'.repeat(26))
    expect(wrapper.text()).toContain('Название — не длиннее 25 символов.')
    expect(name(wrapper).attributes('aria-invalid')).toBe('true')

    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(saveSpy).not.toHaveBeenCalled()
    expect(document.activeElement).toBe(name(wrapper).element)
    wrapper.unmount()
  })

  it('saves the name and the description and says so', async () => {
    const { wrapper, saveSpy } = mountForm()
    await name(wrapper).setValue('Анна К.')
    await description(wrapper).setValue('О книгах')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(saveSpy).toHaveBeenCalledWith({ name: 'Анна К.', description: 'О книгах', avatarId: null })
    expect(wrapper.emitted('saved')?.[0]?.[0]).toMatchObject({ name: 'Анна К.', description: 'О книгах' })
    expect(toast.toasts.map((item) => item.message)).toEqual(['Изменения сохранены'])
    wrapper.unmount()
  })

  it('shows the API field errors under the fields', async () => {
    const { wrapper } = mountForm(async () => ({
      status: 'invalid',
      errors: { description: 'Описание не подходит.' },
    }))
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.text()).toContain('Описание не подходит.')
    expect(document.activeElement).toBe(description(wrapper).element)
    // Typing again checks the field by the rules here, not by the old answer.
    await description(wrapper).setValue('Другое')
    expect(wrapper.text()).not.toContain('Описание не подходит.')
    wrapper.unmount()
  })

  it('keeps what was typed when the save fails, and the button shows the indicator meanwhile', async () => {
    let finish!: (result: ProfileSave) => void
    const { wrapper } = mountForm(() => new Promise((resolve) => (finish = resolve)))
    await name(wrapper).setValue('Анна К.')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    const button = wrapper.get('button[type="submit"]')
    expect(button.attributes('aria-busy')).toBe('true')
    // The fields stay editable while saving.
    expect(name(wrapper).attributes('disabled')).toBeUndefined()

    finish({ status: 'error' })
    await flushPromises()
    expect(button.attributes('aria-busy')).toBeUndefined()
    expect((name(wrapper).element as HTMLInputElement).value).toBe('Анна К.')
    expect(toast.toasts.map((item) => item.message)).toEqual(['Не удалось сохранить. Попробуйте ещё раз.'])
    expect(wrapper.emitted('saved')).toBeUndefined()
    wrapper.unmount()
  })

  it('crops a chosen picture, uploads it on save and saves the profile with its id', async () => {
    const { wrapper, saveSpy, uploadSpy } = mountForm()
    await chooseFile(wrapper, new File(['png'], 'photo.png', { type: 'image/png' }))
    expect(wrapper.find('[role="dialog"]').exists()).toBe(true)
    await wrapper.get('[data-testid="crop-apply"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="avatar-preview"]').element.tagName).toBe('IMG')
    expect(uploadSpy).not.toHaveBeenCalled()

    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(uploadSpy).toHaveBeenCalledWith(cropped, 'avatar.jpg')
    expect(saveSpy).toHaveBeenCalledWith({ name: 'Анна', description: '', avatarId: 'media-1' })
    expect(wrapper.text()).toContain('Убрать аватар')
    wrapper.unmount()
  })

  it('refuses what is not a picture before uploading, and tells why the upload failed', async () => {
    const { wrapper, saveSpy } = mountForm(undefined, async () => ({ status: 'unsupported' }))
    await chooseFile(wrapper, new File(['%PDF'], 'cv.pdf', { type: 'application/pdf' }))
    expect(wrapper.get('[data-testid="avatar-error"]').text()).toContain('JPEG, PNG, WebP или GIF')
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)

    await chooseFile(wrapper, new File(['png'], 'photo.png', { type: 'image/png' }))
    await wrapper.get('[data-testid="crop-apply"]').trigger('click')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.get('[data-testid="avatar-error"]').text()).toContain('формат картинки не подходит')
    expect(saveSpy).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('removes the avatar', async () => {
    const { wrapper, saveSpy } = mountForm(undefined, undefined, { ...anna, avatarId: 'media-0' })
    await wrapper
      .get('form')
      .findAll('button')
      .find((button) => button.text() === 'Убрать аватар')!
      .trigger('click')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(saveSpy).toHaveBeenCalledWith({ name: 'Анна', description: '', avatarId: null })
    expect(wrapper.text()).toContain('Загрузить аватар')
    wrapper.unmount()
  })
})
