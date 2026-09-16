import { describe, expect, it } from 'vitest';
import { shouldLogoutForUnauthorized } from './authFailure';

describe('401 session policy', () => {
  it('logs out when the primary authenticated bootstrap fails', () => {
    expect(shouldLogoutForUnauthorized({
      response: { status: 401 },
      config: { authFailureAction: 'logout' }
    })).toBe(true);
  });

  it('keeps the session when an auxiliary request fails', () => {
    expect(shouldLogoutForUnauthorized({
      response: { status: 401 },
      config: { authFailureAction: 'ignore' }
    })).toBe(false);
    expect(shouldLogoutForUnauthorized({ response: { status: 401 } })).toBe(false);
  });

  it('does not log out for non-401 responses', () => {
    expect(shouldLogoutForUnauthorized({
      response: { status: 403 },
      config: { authFailureAction: 'logout' }
    })).toBe(false);
  });
});
