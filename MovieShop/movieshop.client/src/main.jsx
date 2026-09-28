import React from 'react';
import ReactDOM from 'react-dom/client';
import { GoogleOAuthProvider } from '@react-oauth/google';
import { MotionConfig } from 'motion/react';
import App from './App.jsx';
import { ThemeProvider } from './contexts/ThemeContext';
import './index.css';

const GOOGLE_CLIENT_ID = '790448981257-6s2jg9h067hb0udji4bos1j13aei0vku.apps.googleusercontent.com';

ReactDOM.createRoot(document.getElementById('root')).render(
    <React.StrictMode>
        <GoogleOAuthProvider clientId={GOOGLE_CLIENT_ID}>
            <ThemeProvider>
                {/* A motion-animációk tiszteletben tartják a rendszer „csökkentett mozgás" beállítását */}
                <MotionConfig reducedMotion="user">
                    <App />
                </MotionConfig>
            </ThemeProvider>
        </GoogleOAuthProvider>
    </React.StrictMode>
);
