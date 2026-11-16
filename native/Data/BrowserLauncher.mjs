#!/usr/bin/env node
import * as cua_repl from "@oai/cua-repl";

try {
  await cua_repl.launch();
} catch (error) {
  const error_message = error instanceof Error ? error.message : String(error);
  console.error(`cua_repl could not start: ${error_message}`);
  process.exitCode = 1;
}