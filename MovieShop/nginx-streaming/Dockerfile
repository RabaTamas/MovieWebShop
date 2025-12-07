FROM nginx:alpine

# Install required modules for secure link
RUN apk add --no-cache openssl

# Copy nginx configuration
COPY nginx.conf /etc/nginx/nginx.conf

# Create directory for movie files
RUN mkdir -p /var/www/movies

EXPOSE 8080

CMD ["nginx", "-g", "daemon off;"]
