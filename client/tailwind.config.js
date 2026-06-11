/** @type {import('tailwindcss').Config} */
export default {
  // Class names live in F# string literals; Tailwind's scanner extracts
  // them from any text file, so .fs sources work like .jsx would.
  content: ["./index.html", "./src/**/*.fs"],
  theme: {
    extend: {
      fontFamily: {
        serif: ["Georgia", '"Times New Roman"', "serif"],
      },
    },
  },
  plugins: [],
};
