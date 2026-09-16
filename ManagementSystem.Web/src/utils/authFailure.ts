export type AuthFailureAction = 'logout' | 'ignore';

interface UnauthorizedErrorLike {
  response?: { status?: number };
  config?: { authFailureAction?: AuthFailureAction };
}

/**
 * Only a request explicitly marked as the portal's primary bootstrap may end
 * the current session when the API returns 401. Auxiliary reads (for example
 * payment settings or notifications) must be handled by their caller.
 */
export const shouldLogoutForUnauthorized = (error: UnauthorizedErrorLike | undefined): boolean =>
  error?.response?.status === 401 && error.config?.authFailureAction === 'logout';
