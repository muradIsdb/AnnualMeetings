/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        'isdb-green': '#1a7a4a',
        'isdb-green-dark': '#145f39',
        'isdb-green-light': '#2a9d64',
      },
    },
  },
  plugins: [],
}
