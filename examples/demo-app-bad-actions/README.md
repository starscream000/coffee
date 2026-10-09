# Demo app with bad actions

A project for check F9 (and A7) of the definition of done: its action file
defines `fillOtp` (no namespace), `expect.priceFormat` (reserved namespace) and
`click` (a built-in name). Opening it must report all three and load none.
