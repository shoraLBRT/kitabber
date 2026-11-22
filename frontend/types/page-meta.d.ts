// What a page tells the default layout through definePageMeta.
declare module '#app' {
  interface PageMeta {
    /**
     * `false` hides the phone's floating «Написать» (E07-04, docs/ui/post.md): the post page keeps the place above the
     * bottom bar for its action panel.
     */
    writeButton?: boolean
  }
}

export {}
