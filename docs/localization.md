# Localization

## Shipped languages

| Tag | Direction | Default |
| --- | --- | --- |
| `fa-IR` | RTL | yes |
| `en-US` | LTR | no |

## The string catalogue

`AppStrings` is an abstract strongly-typed catalogue. Each member is `required`, and concrete classes
provide English and Persian values:

- `AppStringsEnglish.Instance`
- `AppStringsPersian.Instance`

A missing Persian or English member prevents compilation. This is preferable to a missing translation
becoming a blank label at runtime.

## Resolving a language

`LanguageService` reads `UserProfile.PreferredLanguage` once per request scope. It caches the tag
for the life of that scope.

Two components can ask for the language during the same render pass. A semaphore serialises the first
database read, so two callers do not run concurrent queries on one `DbContext`.

Unknown or blank tags fall back to Persian.

## Persisting a choice

The choice is persisted on `UserProfile`, not in a cookie. Switching language updates the profile,
sets the in-request value, and forces a full browser reload.

A full reload is required because every component should render from one catalogue. A partial
render can leave the page body in one language and the layout in another.

## HTML direction

`App.razor` sets `lang` and `dir` before a page is rendered:

- Persian: `lang="fa-IR"`, `dir="rtl"`.
- English: `lang="en-US"`, `dir="ltr"`.

## Enum labels

Enum labels are not part of the generic string catalogue. `EnumLabels.Label<TEnum>(value, persian)`
returns the display label for the active language.

This keeps enum names in code stable while making UI labels change.

## Adding a language

1. Add a concrete `AppStrings` implementation.
2. Add the language tag and support branch to `LanguageService`.
3. Add normalization rules for regional variants, if needed.
4. Add enum labels for the new language.
5. Build. The compiler will identify any required catalogue member that is missing.
