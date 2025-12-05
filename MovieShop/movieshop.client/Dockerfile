# ==============================================
# React Frontend Dockerfile
# Multi-stage build: Node.js build + Nginx serve
# ==============================================

# Stage 1: Build Stage
# Use Node.js to build the React application
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files first (for better caching)
# This layer is cached unless package.json or package-lock.json changes
COPY package*.json ./

# Install dependencies
# Use 'npm ci' for faster, more reliable builds in CI/CD
RUN npm ci

# Copy the rest of the application source code
COPY . .

# Build the React application for production
# This creates an optimized build in the 'dist' folder
RUN npm run build

# Stage 2: Production Stage
# Use Nginx to serve the static files
FROM nginx:alpine AS production

# Copy the built React app from the build stage
COPY --from=build /app/dist /usr/share/nginx/html

# Copy custom Nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Expose port 80 for HTTP traffic
EXPOSE 80

# Start Nginx in the foreground
CMD ["nginx", "-g", "daemon off;"]
