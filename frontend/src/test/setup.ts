/* Vitest ortamı için jest-dom eşleştiricilerini ve test sonrası temizliği ayarlar. */
import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

afterEach(() => cleanup());
