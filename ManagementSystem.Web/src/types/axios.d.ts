import 'axios';

declare module 'axios' {
  interface AxiosRequestConfig {
    /** Controls whether a 401 response should end the current session. */
    authFailureAction?: 'logout' | 'ignore';
  }
}
