import { defineConfig } from "vite";
import { aetherisCad } from "@aetheris/cad/vite";

export default defineConfig({
  plugins: [aetherisCad()],
});
