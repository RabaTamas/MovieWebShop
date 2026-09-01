import { useState, useRef, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import './Chatbot.css';
import API_BASE_URL from '../config/api';
import { useAuth } from '../contexts/AuthContext';

// Browser support check
const SpeechRecognitionAPI = window.SpeechRecognition || window.webkitSpeechRecognition;
const speechSupported = !!SpeechRecognitionAPI;
const ttsSupported = 'speechSynthesis' in window;

const Chatbot = () => {
    const { token } = useAuth();
    const navigate = useNavigate();
    const [isOpen, setIsOpen] = useState(false);
    const [messages, setMessages] = useState([
        { text: "👋 Hi! How can I help you?", sender: "bot" }
    ]);
    const [input, setInput] = useState("");
    const [loading, setLoading] = useState(false);

    // Voice input
    const [isListening, setIsListening] = useState(false);
    const recognitionRef = useRef(null);

    // Text-to-speech
    const [ttsEnabled, setTtsEnabled] = useState(false);

    // Language (affects SpeechRecognition + SpeechSynthesis)
    const [lang, setLang] = useState('en-US');
    const toggleLang = () => setLang(prev => prev === 'en-US' ? 'hu-HU' : 'en-US');

    // External trigger: open chatbot and optionally send a message
    useEffect(() => {
        const handler = (e) => {
            setIsOpen(true);
            if (e.detail?.message) {
                setTimeout(() => sendMessage(e.detail.message), 300);
            }
        };
        window.addEventListener('chatbot:open', handler);
        return () => window.removeEventListener('chatbot:open', handler);
    }, []);

    const [activeTab, setActiveTab] = useState('faq'); // 'faq' | 'actions'

    const faqButtons = [
        "What payment methods do you accept?",
        "How do I watch my movies?",
        "Can I get a refund?",
        "How do I contact you?"
    ];

    // Agent action templates: { label, fill, send }
    // fill=true → fills input (user can edit before sending)
    // send=true → sends immediately
    const actionButtons = [
        { icon: '🛒', label: 'Add movie to cart',    template: 'Add [movie] to my cart',    fill: 'Add  to my cart', cursor: 4 },
        { icon: '🗑️', label: 'Remove from cart',     template: 'Remove [movie] from my cart', fill: 'Remove  from my cart', cursor: 7 },
        { icon: '🛒', label: 'Go to cart',            send: 'Go to my cart' },
        { icon: '📦', label: 'Go to orders',         send: 'Go to my orders' },
        { icon: '👤', label: 'Go to profile',        send: 'Go to my profile' },
        { icon: '🏠', label: 'Go to home',           send: 'Go to home page' },
        { icon: '🎬', label: 'Go to my movies',      send: 'Go to my movies' },
        { icon: '🔨', label: 'Go to auctions',       send: 'Go to auctions' },
        { icon: '✏️', label: 'Change my name',        fill: 'Change my name to ' },
        { icon: '🔍', label: 'Search movie',          fill: 'Search for ' },
        { icon: '▶️', label: 'Watch movie',           fill: 'Watch ' },
        { icon: '🎉', label: 'Watch party',           fill: 'Start watch party for ' },
        { icon: '📮', label: 'Set billing address',   fill: 'Set my billing address to [street], [city], [zip]', cursor: 33 },
    ];

    const sessionId = (() => {
        let id = localStorage.getItem('chatSessionId');
        if (!id) {
            id = `session_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
            localStorage.setItem('chatSessionId', id);
        }
        return id;
    })();

    // Stop TTS and recognition on unmount / window close
    useEffect(() => {
        return () => {
            recognitionRef.current?.abort();
            window.speechSynthesis?.cancel();
        };
    }, []);

    // ── Text-to-Speech ────────────────────────────────────────────
    const speakText = (text) => {
        if (!ttsSupported || !ttsEnabled) return;
        window.speechSynthesis.cancel();
        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = lang;
        utterance.rate = 1.0;
        utterance.pitch = 1.0;
        window.speechSynthesis.speak(utterance);
    };

    const toggleTts = () => {
        if (ttsEnabled) window.speechSynthesis?.cancel();
        setTtsEnabled(prev => !prev);
    };

    // ── Speech Recognition ────────────────────────────────────────
    const startListening = () => {
        if (!speechSupported || isListening) return;

        const recognition = new SpeechRecognitionAPI();
        recognition.lang = lang;
        recognition.interimResults = false;
        recognition.maxAlternatives = 1;

        recognition.onstart = () => setIsListening(true);

        recognition.onresult = (event) => {
            const transcript = event.results[0][0].transcript;
            setInput(transcript);
            setIsListening(false);
            // Auto-send after brief delay so user sees the text
            setTimeout(() => sendMessage(transcript), 300);
        };

        recognition.onerror = () => setIsListening(false);
        recognition.onend = () => setIsListening(false);

        recognitionRef.current = recognition;
        recognition.start();
    };

    const stopListening = () => {
        recognitionRef.current?.stop();
        setIsListening(false);
    };

    // ── Handle agent actions from backend ────────────────────────
    const handleAction = (action) => {
        if (!action) return;
        if (action.type === 'navigate') {
            // Direct path (e.g. /my-movies/5/watch) takes priority over page name
            if (action.payload?.path) {
                setTimeout(() => navigate(action.payload.path), 800);
                return;
            }
            const pageMap = {
                cart: '/cart', checkout: '/cart', profile: '/profile',
                orders: '/orders', home: '/', 'my-movies': '/my-movies',
                auctions: '/auctions'
            };
            const path = pageMap[action.payload?.page] ?? '/';
            setTimeout(() => navigate(path), 800);
        }
        // cart_updated and profile_updated are handled server-side;
        // the bot's text response already confirms the action.
    };

    // ── Send message ──────────────────────────────────────────────
    const sendMessage = async (text) => {
        const trimmed = text?.trim();
        if (!trimmed) return;

        recognitionRef.current?.abort();
        setIsListening(false);

        setMessages(prev => [...prev, { text: trimmed, sender: "user" }]);
        setInput("");
        setLoading(true);

        try {
            const headers = { 'Content-Type': 'application/json' };
            if (token) headers['Authorization'] = `Bearer ${token}`;

            const response = await fetch(`${API_BASE_URL}/api/Chat/ask`, {
                method: 'POST',
                headers,
                body: JSON.stringify({ question: trimmed, sessionId })
            });

            if (!response.ok) throw new Error('Network response was not ok');

            const data = await response.json();
            console.log('[Chatbot] response:', JSON.stringify(data));
            const botMessage = {
                text: data.answer,
                sender: "bot",
                source: data.source,
                action: data.action ?? null
            };
            setMessages(prev => [...prev, botMessage]);
            speakText(data.answer);
            handleAction(data.action);
        } catch (err) {
            console.error('Chat error:', err);
            const errMsg = "😔 An error occurred. Please try again or email us: support@movieshop.com";
            setMessages(prev => [...prev, { text: errMsg, sender: "bot" }]);
            speakText(errMsg);
        } finally {
            setLoading(false);
        }
    };

    const handleKeyPress = (e) => {
        if (e.key === 'Enter' && input.trim() && !loading) sendMessage(input);
    };

    return (
        <>
            {/* Floating bubble */}
            {!isOpen && (
                <button className="chatbot-bubble" onClick={() => setIsOpen(true)} aria-label="Open chat">
                    💬 How can I help?
                </button>
            )}

            {/* Chat window */}
            {isOpen && (
                <div className="chatbot-window">
                    {/* Header */}
                    <div className="chatbot-header">
                        <div>
                            <h5 className="mb-0">🎬 MovieShop Assistant</h5>
                            <small>Usually responds in 1 minute</small>
                        </div>
                        <div className="chatbot-header-actions">
                            {/* Language toggle */}
                            <button
                                onClick={toggleLang}
                                title={`Switch to ${lang === 'en-US' ? 'Hungarian' : 'English'}`}
                                className="lang-btn"
                            >
                                {lang === 'en-US' ? 'EN' : 'HU'}
                            </button>
                            {/* TTS toggle */}
                            {ttsSupported && (
                                <button
                                    onClick={toggleTts}
                                    aria-label={ttsEnabled ? "Disable voice" : "Enable voice"}
                                    title={ttsEnabled ? "Voice responses: ON" : "Voice responses: OFF"}
                                    className={ttsEnabled ? 'tts-active' : ''}
                                >
                                    {ttsEnabled ? '🔊' : '🔇'}
                                </button>
                            )}
                            <button onClick={() => { setIsOpen(false); window.speechSynthesis?.cancel(); }} aria-label="Close chat">
                                ✕
                            </button>
                        </div>
                    </div>

                    {/* Messages */}
                    <div className="chatbot-messages">
                        {messages.map((msg, idx) => (
                            <div key={idx} className={`message ${msg.sender}`}>
                                <div className="message-bubble">
                                    {msg.text}
                                    {msg.source && (
                                        <small className="message-source">
                                            {msg.source === 'FAQ' ? '📚 FAQ' : msg.source === 'agent' ? '⚡ Agent' : '🤖 AI'}
                                        </small>
                                    )}
                                    {msg.action?.type === 'cart_updated' && (
                                        <div className="mt-2">
                                            <a href="/cart" className="btn btn-sm btn-outline-primary">🛒 View Cart</a>
                                        </div>
                                    )}
                                    {msg.action?.type === 'navigate' && (
                                        <small className="message-source">↗ Navigating...</small>
                                    )}
                                </div>
                            </div>
                        ))}
                        {loading && (
                            <div className="message bot">
                                <div className="message-bubble">
                                    <div className="typing-indicator">
                                        <span></span><span></span><span></span>
                                    </div>
                                </div>
                            </div>
                        )}
                    </div>

                    {/* Quick actions panel */}
                    <div className="chatbot-faq">
                        <div className="chatbot-tabs">
                            <button
                                className={`chatbot-tab ${activeTab === 'faq' ? 'active' : ''}`}
                                onClick={() => setActiveTab('faq')}
                            >
                                💬 FAQ
                            </button>
                            {token && (
                                <button
                                    className={`chatbot-tab ${activeTab === 'actions' ? 'active' : ''}`}
                                    onClick={() => setActiveTab('actions')}
                                >
                                    ⚡ Actions
                                </button>
                            )}
                        </div>

                        {activeTab === 'faq' && faqButtons.map((q, idx) => (
                            <button
                                key={idx}
                                className="btn btn-sm btn-outline-primary"
                                onClick={() => sendMessage(q)}
                                disabled={loading}
                            >
                                {q}
                            </button>
                        ))}

                        {activeTab === 'actions' && token && (
                            <div className="action-buttons">
                                {actionButtons.map((a, idx) => (
                                    <button
                                        key={idx}
                                        className="btn btn-sm btn-outline-secondary action-btn"
                                        disabled={loading}
                                        onClick={() => {
                                            if (a.send) {
                                                sendMessage(a.send);
                                            } else {
                                                setInput(a.fill);
                                                setTimeout(() => {
                                                    const inp = document.querySelector('.chatbot-input input');
                                                    if (inp) {
                                                        inp.focus();
                                                        if (a.cursor != null) inp.setSelectionRange(a.cursor, a.cursor);
                                                    }
                                                }, 50);
                                            }
                                        }}
                                        title={a.template ?? a.send}
                                    >
                                        {a.icon} {a.label}
                                        {a.fill && <span className="action-hint"> →</span>}
                                    </button>
                                ))}
                                <small className="text-muted mt-2 d-block">
                                    → fills input &nbsp;|&nbsp; no arrow = sends directly
                                </small>
                            </div>
                        )}
                    </div>

                    {/* Input area */}
                    <div className="chatbot-input">
                        <input
                            type="text"
                            placeholder={isListening ? "Listening..." : "Ask a question..."}
                            value={input}
                            onChange={(e) => setInput(e.target.value)}
                            onKeyPress={handleKeyPress}
                            disabled={loading || isListening}
                        />

                        {/* Microphone button */}
                        {speechSupported && (
                            <button
                                className={`mic-btn ${isListening ? 'listening' : ''}`}
                                onClick={isListening ? stopListening : startListening}
                                disabled={loading}
                                aria-label={isListening ? "Stop listening" : "Start voice input"}
                                title={isListening ? "Click to stop" : "Voice input"}
                            >
                                🎤
                            </button>
                        )}

                        {/* Send button */}
                        <button
                            onClick={() => input.trim() && sendMessage(input)}
                            disabled={loading || !input.trim() || isListening}
                            aria-label="Send message"
                        >
                            ➤
                        </button>
                    </div>

                    {/* Listening status bar */}
                    {isListening && (
                        <div className="listening-bar">
                            <span className="listening-dot" />
                            Listening... speak now
                        </div>
                    )}
                </div>
            )}
        </>
    );
};

export default Chatbot;
