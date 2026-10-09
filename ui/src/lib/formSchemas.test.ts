import { nullIfEmpty, optionalEmail, optionalPhone, optionalText, phoneNumber, requiredEmail, requiredText } from './formSchemas';

describe('form schemas', () => {
  it('trims required text and rejects blanks', () => {
    expect(requiredText('Name', 10).parse('  Yoga ')).toBe('Yoga');
    expect(requiredText('Name', 10).safeParse('   ').error?.issues[0].message).toBe('Name is required');
  });

  it('trims optional text and email, and accepts them empty', () => {
    expect(optionalText('Middle name', 10).parse('  ')).toBe('');
    expect(optionalEmail(254).parse('')).toBe('');
    expect(optionalEmail(254).parse(' olena@example.com ')).toBe('olena@example.com');
    expect(optionalEmail(254).safeParse('olena@').error?.issues[0].message).toBe('Enter a valid email');
    expect(nullIfEmpty('')).toBeNull();
    expect(nullIfEmpty('x')).toBe('x');
  });

  it('accepts phone numbers the API accepts', () => {
    expect(phoneNumber(32).parse('+380 (67) 123-45-67')).toBe('+380 (67) 123-45-67');
    expect(phoneNumber(32).safeParse('12345').error?.issues[0].message).toBe('Phone number must have 10 to 15 digits');
    expect(phoneNumber(32).safeParse('call me').error?.issues[0].message).toBe('Use digits, spaces, dashes, parentheses and a leading +');
  });

  it('requires an email when the email is the primary contact', () => {
    expect(requiredEmail(254).parse(' olena@example.com ')).toBe('olena@example.com');
    expect(requiredEmail(254).safeParse('  ').error?.issues[0].message).toBe('Email is required');
    expect(requiredEmail(254).safeParse('olena@').error?.issues[0].message).toBe('Enter a valid email');
  });

  it('accepts an empty optional phone and checks a filled one', () => {
    expect(optionalPhone(32).parse('  ')).toBe('');
    expect(optionalPhone(32).parse('+380 (67) 123-45-67')).toBe('+380 (67) 123-45-67');
    expect(optionalPhone(32).safeParse('12345').error?.issues[0].message).toBe('Phone number must have 10 to 15 digits');
    expect(optionalPhone(32).safeParse('call me').error?.issues[0].message).toBe('Use digits, spaces, dashes, parentheses and a leading +');
  });
});
