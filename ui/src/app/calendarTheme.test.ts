import mainSource from '../main.tsx?raw';
import theme from './calendarTheme.css?raw';

const rules = [...theme.replace(/\/\*[\s\S]*?\*\//g, '').matchAll(/([^{}]+)\{([^{}]*)\}/g)].map(([, selectors, body]) => ({
  selectors: selectors.split(',').map((selector) => selector.replace(/\s+/g, ' ').trim()),
  body,
}));

const themed = [
  '.rbc-toolbar button',
  '.rbc-toolbar button:hover',
  '.rbc-toolbar button:focus',
  '.rbc-toolbar button:active',
  '.rbc-toolbar button.rbc-active',
  '.rbc-today',
  '.rbc-off-range',
  '.rbc-off-range-bg',
  '.rbc-overlay',
  '.rbc-show-more',
  '.rbc-header',
  '.rbc-month-view',
  '.rbc-time-view',
  '.rbc-time-content',
  '.rbc-timeslot-group',
  '.rbc-day-slot .rbc-time-slot',
  '.rbc-day-bg + .rbc-day-bg',
];

describe('calendar theme', () => {
  it.each(themed)('%s takes its colors from the Mantine theme, so it is readable in dark mode', (selector) => {
    const rule = rules.find((r) => r.selectors.includes(selector));
    expect(rule?.body).toMatch(/var\(--mantine-/);
  });

  it('is loaded after the react-big-calendar styles it overrides', () => {
    const library = mainSource.indexOf("import 'react-big-calendar/lib/css/react-big-calendar.css';");
    const override = mainSource.indexOf("import './app/calendarTheme.css';");
    expect(library).toBeGreaterThan(-1);
    expect(override).toBeGreaterThan(library);
  });
});
