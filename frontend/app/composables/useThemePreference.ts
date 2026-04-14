const COOKIE_OPTS = { maxAge: 60 * 60 * 24 * 365, sameSite: 'lax' as const, path: '/' }

const VALID_PRIMARIES = [
  'red', 'orange', 'amber', 'yellow', 'lime', 'green', 'emerald', 'teal',
  'cyan', 'sky', 'blue', 'indigo', 'violet', 'purple', 'fuchsia', 'pink', 'rose'
]
const VALID_NEUTRALS = ['slate', 'gray', 'zinc', 'neutral', 'stone']

export const useThemePreference = () => {
  const appConfig = useAppConfig()

  const primaryCookie = useCookie<string>('ph_primary', COOKIE_OPTS)
  const neutralCookie = useCookie<string>('ph_neutral', COOKIE_OPTS)

  function restore() {
    if (primaryCookie.value && VALID_PRIMARIES.includes(primaryCookie.value)) {
      appConfig.ui.colors.primary = primaryCookie.value
    }
    if (neutralCookie.value && VALID_NEUTRALS.includes(neutralCookie.value)) {
      appConfig.ui.colors.neutral = neutralCookie.value
    }
  }

  function setPrimary(color: string) {
    appConfig.ui.colors.primary = color
    primaryCookie.value = color
  }

  function setNeutral(color: string) {
    appConfig.ui.colors.neutral = color
    neutralCookie.value = color
  }

  return { restore, setPrimary, setNeutral }
}
