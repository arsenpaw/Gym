import { screen, within } from '@testing-library/react';
import { signInAs, testAuth } from '../test/auth';
import { renderRoute } from '../test/render';
import { AppLayout } from './AppLayout';

const renderLayout = () => renderRoute([{ path: '/', element: <AppLayout />, children: [{ index: true, element: <p>content</p> }] }], '/');

const navLabels = () => within(screen.getByRole('navigation')).getAllByRole('link').map((link) => link.textContent);

describe('AppLayout', () => {
  it('shows an admin every section', async () => {
    signInAs('Admin');
    renderLayout();

    expect(await screen.findByRole('link', { name: 'Reports' })).toBeInTheDocument();
    expect(navLabels()).toEqual(['Check-in', 'Clients', 'Schedule', 'Trainers', 'Rooms', 'Membership plans', 'Notifications', 'Reports']);
  });

  it('shows a trainer only the schedule and rooms', async () => {
    signInAs('Trainer');
    renderLayout();

    expect(await screen.findByRole('link', { name: 'Schedule' })).toBeInTheDocument();
    expect(navLabels()).toEqual(['Schedule', 'Rooms']);
  });

  it('signs out through Auth0', async () => {
    signInAs('Receptionist');
    const { user } = renderLayout();

    await user.click(screen.getByRole('button', { name: 'Account menu' }));
    await user.click(await screen.findByRole('menuitem', { name: 'Sign out' }));

    expect(testAuth.logout).toHaveBeenCalledWith({ logoutParams: { returnTo: window.location.origin } });
  });
});
