/* Tailwind tema, renk ve içerik tarama ayarlarını tanımlar. */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        brand: { 50: '#fffbea', 100: '#fff4b8', 200: '#ffe77a', 300: '#ffdc3d', 400: '#ffd91a', 500: '#ffd200', 600: '#efbd00', 700: '#ad8300' },
        ink: '#1f3b56',
        surface: '#f6f6f6',
      },
      boxShadow: { card: '0 2px 8px rgba(31, 59, 86, 0.08)' },
    },
  },
  plugins: [],
};
