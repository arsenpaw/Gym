import { fakeAccessToken } from '../test/auth';
import { hasAnyRole, readRoles } from './roles';

describe('readRoles', () => {
  it('reads the club roles from the access token claim', () => {
    expect(readRoles(fakeAccessToken(['Admin', 'Trainer']))).toEqual(['Admin', 'Trainer']);
  });

  it('ignores roles the UI does not know', () => {
    const token = fakeAccessToken(['Receptionist', 'Owner' as never]);
    expect(readRoles(token)).toEqual(['Receptionist']);
  });

  it('returns no roles for a missing or malformed token', () => {
    expect(readRoles(undefined)).toEqual([]);
    expect(readRoles('not-a-jwt')).toEqual([]);
  });
});

describe('hasAnyRole', () => {
  it('is true when one of the allowed roles is present', () => {
    expect(hasAnyRole(['Receptionist'], ['Admin', 'Receptionist'])).toBe(true);
    expect(hasAnyRole(['Trainer'], ['Admin'])).toBe(false);
  });
});
