export type UserRole = 'Admin' | 'Lecture' | 'User';
export type PortalType = 'Admin' | 'Lecture' | 'User';

export interface UserInfo {
  id?: string;
  fullName?: string;
  email?: string;
  phoneNumber?: string;
}

export interface SessionData {
  token: string | null;
  role: UserRole | null;
  roles: UserRole[];
  user: UserInfo | null;
  activePortal: PortalType | null;
}

export const clearSession = (): void => {
  localStorage.removeItem('token');
  localStorage.removeItem('role');
  localStorage.removeItem('roles');
  localStorage.removeItem('user');
  localStorage.removeItem('fullName');
  localStorage.removeItem('activePortal');
};

export const getSession = (): SessionData => {
  const token = localStorage.getItem('token');
  const role = (localStorage.getItem('role') as UserRole) || null;
  const rolesJson = localStorage.getItem('roles');
  let roles: UserRole[] = [];
  try {
    roles = rolesJson ? JSON.parse(rolesJson) : (role ? [role] : []);
  } catch {
    roles = role ? [role] : [];
  }

  let user: UserInfo | null = null;
  try {
    const userJson = localStorage.getItem('user');
    user = userJson ? JSON.parse(userJson) : null;
  } catch {
    user = null;
  }

  let activePortal: PortalType | null = null;
  const savedPortal = localStorage.getItem('activePortal') as PortalType;

  if (roles.includes('Admin')) {
    activePortal = 'Admin';
  } else if (roles.includes('Lecture') && roles.includes('User')) {
    if (savedPortal === 'Lecture' || savedPortal === 'User') {
      activePortal = savedPortal;
    } else {
      activePortal = role === 'User' ? 'User' : 'Lecture';
    }
  } else if (roles.includes('Lecture')) {
    activePortal = 'Lecture';
  } else if (roles.includes('User')) {
    activePortal = 'User';
  }

  return { token, role, roles, user, activePortal };
};

export const setSession = (data: {
  token: string;
  role: string;
  roles?: string[];
  user?: UserInfo;
}): SessionData => {
  localStorage.setItem('token', data.token);
  localStorage.setItem('role', data.role);

  const roles = (data.roles && Array.isArray(data.roles) && data.roles.length > 0)
    ? data.roles as UserRole[]
    : [data.role as UserRole];
  localStorage.setItem('roles', JSON.stringify(roles));

  if (data.user) {
    localStorage.setItem('user', JSON.stringify(data.user));
    if (data.user.fullName) {
      localStorage.setItem('fullName', data.user.fullName);
    }
  }

  // Determine initial active portal
  let activePortal: PortalType = 'User';
  if (roles.includes('Admin')) {
    activePortal = 'Admin';
  } else if (roles.includes('Lecture') && roles.includes('User')) {
    // For dual role, default to Lecture (or data.role if specified)
    activePortal = data.role === 'User' ? 'User' : 'Lecture';
  } else if (roles.includes('Lecture')) {
    activePortal = 'Lecture';
  } else {
    activePortal = 'User';
  }

  localStorage.setItem('activePortal', activePortal);

  return {
    token: data.token,
    role: data.role as UserRole,
    roles,
    user: data.user || null,
    activePortal
  };
};

export const setActivePortal = (portal: PortalType): void => {
  const { roles } = getSession();
  if (roles.includes('Admin')) {
    localStorage.setItem('activePortal', 'Admin');
    return;
  }

  if (portal === 'Lecture' && roles.includes('Lecture')) {
    localStorage.setItem('activePortal', 'Lecture');
  } else if (portal === 'User' && roles.includes('User')) {
    localStorage.setItem('activePortal', 'User');
  }
};
