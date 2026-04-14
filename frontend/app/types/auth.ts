export interface SsoUser {
  id: string
  name: string
  email: string
  firstName: string
  lastName: string
  groups: string[]
  loginTime: string
  tenantKey: string
  tenantDisplayName: string
}
