import { Paper } from '@mantine/core';
import { useElementSize } from '@mantine/hooks';
import { useCallback, useEffect, useRef, useState } from 'react';

const minHeight = 200;

export const EmailPreviewFrame = ({ html }: { html: string }) => {
  const frame = useRef<HTMLIFrameElement>(null);
  const { ref: wrapper, width } = useElementSize();
  const [height, setHeight] = useState(minHeight);

  const fitToContent = useCallback(() => {
    const body = frame.current?.contentDocument?.body;
    if (body) setHeight(Math.max(minHeight, body.scrollHeight));
  }, []);

  useEffect(fitToContent, [fitToContent, width]);

  return (
    <Paper ref={wrapper} radius="md" withBorder style={{ overflow: 'hidden' }}>
      <iframe
        ref={frame}
        title="Email preview"
        srcDoc={html}
        sandbox="allow-same-origin"
        scrolling="no"
        onLoad={fitToContent}
        style={{ display: 'block', width: '100%', height, border: 0 }}
      />
    </Paper>
  );
};
