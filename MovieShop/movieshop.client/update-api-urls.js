#!/usr/bin/env node
/**
 * Script to replace hardcoded API URLs with centralized API_BASE_URL
 * across all frontend files
 */

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const filesToUpdate = [
  'src/components/Chatbot.jsx',
  'src/components/MovieCategories.jsx',
  'src/components/MovieForm.jsx',
  'src/components/ReviewList.jsx',
  'src/pages/AdminAddresses.jsx',
  'src/pages/AdminCategories.jsx',
  'src/pages/AdminMovies.jsx',
  'src/pages/AdminReviews.jsx',
  'src/pages/AdminShoppingCarts.jsx',
  'src/pages/AdminUsers.jsx',
  'src/pages/Cart.jsx',
  'src/pages/Home.jsx',
  'src/pages/Login.jsx',
  'src/pages/MovieDetails.jsx',
  'src/pages/Orders.jsx',
  'src/pages/Profile.jsx',
  'src/pages/Register.jsx',
  'src/services/orderService.js',
];

const oldUrl = 'https://localhost:7289';
const importStatement = "import API_BASE_URL from '../config/api';";
const importStatementForServices = "import API_BASE_URL from '../config/api';";

let updatedCount = 0;
let errorCount = 0;

filesToUpdate.forEach((file) => {
  const filePath = path.join(__dirname, file);

  try {
    if (!fs.existsSync(filePath)) {
      console.error(`❌ File not found: ${file}`);
      errorCount++;
      return;
    }

    let content = fs.readFileSync(filePath, 'utf8');

    // Check if file contains the old URL
    if (!content.includes(oldUrl)) {
      console.log(`⏭️  Skipping ${file} (no hardcoded URLs found)`);
      return;
    }

    // Replace all occurrences of the hardcoded URL
    const updatedContent = content.replace(
      new RegExp(oldUrl, 'g'),
      '${API_BASE_URL}'
    );

    // Add import statement if not already present
    let finalContent = updatedContent;
    if (!finalContent.includes("import API_BASE_URL from")) {
      // Determine the correct import path based on file location
      const isService = file.includes('/services/');
      const correctImport = isService ? importStatementForServices : importStatement;

      // Find the last import statement
      const lines = finalContent.split('\n');
      let lastImportIndex = -1;

      for (let i = 0; i < lines.length; i++) {
        if (lines[i].trim().startsWith('import ')) {
          lastImportIndex = i;
        } else if (lastImportIndex >= 0 && lines[i].trim() !== '') {
          // Found first non-import, non-empty line after imports
          break;
        }
      }

      if (lastImportIndex >= 0) {
        // Insert after last import
        lines.splice(lastImportIndex + 1, 0, correctImport);
        finalContent = lines.join('\n');
      } else {
        // No imports found, add at the beginning
        finalContent = correctImport + '\n\n' + finalContent;
      }
    }

    // Write the updated content back
    fs.writeFileSync(filePath, finalContent, 'utf8');
    console.log(`✅ Updated ${file}`);
    updatedCount++;

  } catch (error) {
    console.error(`❌ Error processing ${file}:`, error.message);
    errorCount++;
  }
});

console.log(`\n${'='.repeat(50)}`);
console.log(`✅ Successfully updated: ${updatedCount} files`);
if (errorCount > 0) {
  console.log(`❌ Errors: ${errorCount} files`);
}
console.log(`${'='.repeat(50)}\n`);
