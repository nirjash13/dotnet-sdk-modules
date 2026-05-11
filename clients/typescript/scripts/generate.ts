#!/usr/bin/env node
/**
 * Invokes NSwag CLI to generate TypeScript client from the SaasBuilder OpenAPI spec.
 *
 * Usage:
 *   npx tsx scripts/generate.ts [--url http://localhost:5000/openapi/v1.json]
 */
import { execSync } from "node:child_process";
import { existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const nswagConfig = path.join(root, "nswag.json");

if (!existsSync(nswagConfig)) {
  console.error(`nswag.json not found at ${nswagConfig}`);
  process.exit(1);
}

const url =
  process.argv.find((a) => a.startsWith("--url="))?.slice("--url=".length) ??
  "http://localhost:5000/openapi/v1.json";

console.log(`Generating TypeScript client from ${url}...`);

try {
  execSync(`npx nswag run ${nswagConfig} /runtime:Net80`, {
    cwd: root,
    stdio: "inherit",
    env: {
      ...process.env,
      NSWAG_INPUT_URL: url,
    },
  });
  console.log("Generation complete. Output: src/generated/api.ts");
} catch (err) {
  console.error("NSwag generation failed:", err);
  process.exit(1);
}
