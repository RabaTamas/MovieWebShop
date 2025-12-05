// API Base URL Configuration
// This uses Vite's environment variable system
// In production (Docker), VITE_API_URL is set at build time
// In development, it defaults to the local backend

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000';

export default API_BASE_URL;
