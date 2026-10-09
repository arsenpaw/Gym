type ClientContact = { fullName: string; email: string; phone?: string | null };

export const clientContacts = (client: ClientContact) => [client.email, client.phone].filter(Boolean).join(' · ');

export const clientOptionLabel = (client: ClientContact) => `${client.fullName} · ${clientContacts(client)}`;
