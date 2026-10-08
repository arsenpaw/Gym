import '@fontsource-variable/inter';
import '@mantine/core/styles.layer.css';
import '@mantine/dates/styles.layer.css';
import '@mantine/charts/styles.layer.css';
import '@mantine/notifications/styles.layer.css';
import 'mantine-datatable/styles.layer.css';
import 'react-big-calendar/lib/css/react-big-calendar.css';
import './app/calendarTheme.css';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './app/App';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
