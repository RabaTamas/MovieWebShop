import { useState, useRef, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
    ArrowUpRight, BookOpen, Bot, Clapperboard, MessageCircle, Mic, MicOff, SendHorizontal, ShoppingCart, Volume2, VolumeX, X, Zap,
} from 'lucide-react';
import API_BASE_URL from '../config/api';
import { useAuth } from '../contexts/AuthContext';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';

// Browser support check
const SpeechRecognitionAPI = window.SpeechRecognition || window.webkitSpeechRecognition;
const speechSupported = !!SpeechRecognitionAPI;
const ttsSupported = 'speechSynthesis' in window;

const SourceTag = ({ source }) => {
    const map = {
        FAQ: { icon: BookOpen, label: 'FAQ' },
        agent: { icon: Zap, label: 'Agent' },
    };
    const { icon: Icon, label } = map[source] ?? { icon: Bot, label: 'AI' };
    return (
        <span className="mt-1.5 flex items-center gap-1 text-[10px] font-medium tracking-wide text-muted-foreground uppercase">
            <Icon className="size-3" />{label}
        </span>
    );
};

const Chatbot = () => {
    const { token } = useAuth();
    const navigate = useNavigate();
    const [isOpen, setIsOpen] = useState(false);
    const [messages, setMessages] = useState([
        { text: "👋 Hi! How can I help you?", sender: "bot" }
    ]);
    const [input, setInput] = useState("");
    const [loading, setLoading] = useState(false);
    const inputRef = useRef(null);
    const messagesEndRef = useRef(null);

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

    // Keep the newest message in view
    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }, [messages, loading, isOpen]);

    const [activeTab, setActiveTab] = useState('faq'); // 'faq' | 'actions'

    const faqButtons = [
        "What payment methods do you accept?",
        "How do I watch my movies?",
        "Can I get a refund?",
        "How do I contact you?"
    ];

    // Agent action templates: { label, fill, send }
    // fill → fills input (user can edit before sending)
    // send → sends immediately
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

    const handleKeyDown = (e) => {
        if (e.key === 'Enter' && input.trim() && !loading) sendMessage(input);
    };

    const headerIconButton = "flex size-8 cursor-pointer items-center justify-center rounded-md text-primary-foreground/80 transition-colors hover:bg-black/10 hover:text-primary-foreground";

    return (
        <>
            {/* Floating bubble */}
            {!isOpen && (
                <button
                    className="fixed right-5 bottom-5 z-50 flex cursor-pointer items-center gap-2 rounded-full bg-primary py-3 pr-5 pl-4 font-semibold text-primary-foreground shadow-2xl shadow-primary/30 transition-all hover:-translate-y-0.5 hover:shadow-primary/50"
                    onClick={() => setIsOpen(true)}
                    aria-label="Open chat"
                >
                    <MessageCircle className="size-5" />
                    <span className="hidden sm:inline">How can I help?</span>
                </button>
            )}

            {/* Chat window */}
            {isOpen && (
                <div className="fixed inset-x-3 bottom-3 z-50 flex h-[min(640px,calc(100dvh-1.5rem))] animate-in flex-col overflow-hidden rounded-2xl border bg-popover text-popover-foreground shadow-2xl duration-200 fade-in slide-in-from-bottom-4 sm:inset-x-auto sm:right-5 sm:bottom-5 sm:w-[400px]">
                    {/* Header */}
                    <div className="flex items-center justify-between gap-2 bg-primary px-4 py-3 text-primary-foreground">
                        <div className="flex items-center gap-3">
                            <span className="flex size-9 items-center justify-center rounded-full bg-black/15">
                                <Clapperboard className="size-5" />
                            </span>
                            <div>
                                <h2 className="leading-tight font-semibold">MovieShop Assistant</h2>
                                <p className="flex items-center gap-1.5 text-xs opacity-80">
                                    <span className="size-1.5 rounded-full bg-emerald-600" />
                                    Usually responds in 1 minute
                                </p>
                            </div>
                        </div>
                        <div className="flex items-center gap-0.5">
                            {/* Language toggle */}
                            <button
                                onClick={toggleLang}
                                title={`Switch to ${lang === 'en-US' ? 'Hungarian' : 'English'}`}
                                className={cn(headerIconButton, "w-auto px-2 text-xs font-bold")}
                            >
                                {lang === 'en-US' ? 'EN' : 'HU'}
                            </button>
                            {/* TTS toggle */}
                            {ttsSupported && (
                                <button
                                    onClick={toggleTts}
                                    aria-label={ttsEnabled ? "Disable voice" : "Enable voice"}
                                    title={ttsEnabled ? "Voice responses: ON" : "Voice responses: OFF"}
                                    className={cn(headerIconButton, ttsEnabled && "bg-black/15 text-primary-foreground")}
                                >
                                    {ttsEnabled ? <Volume2 className="size-4" /> : <VolumeX className="size-4" />}
                                </button>
                            )}
                            <button
                                onClick={() => { setIsOpen(false); window.speechSynthesis?.cancel(); }}
                                aria-label="Close chat"
                                className={headerIconButton}
                            >
                                <X className="size-4" />
                            </button>
                        </div>
                    </div>

                    {/* Messages */}
                    <div className="min-h-0 flex-1 space-y-3 overflow-y-auto bg-background/40 px-4 py-4">
                        {messages.map((msg, idx) => (
                            <div key={idx} className={cn("flex", msg.sender === 'user' ? "justify-end" : "justify-start")}>
                                <div className={cn(
                                    "max-w-[85%] rounded-2xl px-3.5 py-2 text-sm leading-relaxed whitespace-pre-line",
                                    msg.sender === 'user'
                                        ? "rounded-br-sm bg-primary text-primary-foreground"
                                        : "rounded-bl-sm border bg-card text-card-foreground"
                                )}>
                                    {msg.text}
                                    {msg.source && <SourceTag source={msg.source} />}
                                    {msg.action?.type === 'cart_updated' && (
                                        <div className="mt-2">
                                            <Button size="sm" variant="outline" onClick={() => navigate('/cart')}>
                                                <ShoppingCart /> View Cart
                                            </Button>
                                        </div>
                                    )}
                                    {msg.action?.type === 'navigate' && (
                                        <span className="mt-1 flex items-center gap-1 text-[10px] font-medium tracking-wide text-muted-foreground uppercase">
                                            <ArrowUpRight className="size-3" /> Navigating...
                                        </span>
                                    )}
                                </div>
                            </div>
                        ))}
                        {loading && (
                            <div className="flex justify-start">
                                <div className="flex items-center gap-1 rounded-2xl rounded-bl-sm border bg-card px-4 py-3" aria-label="Typing">
                                    <span className="size-1.5 animate-bounce rounded-full bg-muted-foreground [animation-delay:-0.3s]" />
                                    <span className="size-1.5 animate-bounce rounded-full bg-muted-foreground [animation-delay:-0.15s]" />
                                    <span className="size-1.5 animate-bounce rounded-full bg-muted-foreground" />
                                </div>
                            </div>
                        )}
                        <div ref={messagesEndRef} />
                    </div>

                    {/* Quick actions panel */}
                    <div className="border-t bg-card/60 px-3 pt-2 pb-3">
                        <div className="mb-2 flex gap-1">
                            <button
                                className={cn(
                                    "flex cursor-pointer items-center gap-1.5 rounded-md px-2.5 py-1 text-xs font-medium transition-colors",
                                    activeTab === 'faq' ? "bg-accent text-accent-foreground" : "text-muted-foreground hover:text-foreground"
                                )}
                                onClick={() => setActiveTab('faq')}
                            >
                                <BookOpen className="size-3.5" /> FAQ
                            </button>
                            {token && (
                                <button
                                    className={cn(
                                        "flex cursor-pointer items-center gap-1.5 rounded-md px-2.5 py-1 text-xs font-medium transition-colors",
                                        activeTab === 'actions' ? "bg-accent text-accent-foreground" : "text-muted-foreground hover:text-foreground"
                                    )}
                                    onClick={() => setActiveTab('actions')}
                                >
                                    <Zap className="size-3.5" /> Actions
                                </button>
                            )}
                        </div>

                        {activeTab === 'faq' && (
                            <div className="scrollbar-none flex gap-1.5 overflow-x-auto">
                                {faqButtons.map((q, idx) => (
                                    <button
                                        key={idx}
                                        className="shrink-0 cursor-pointer rounded-full border px-3 py-1 text-xs whitespace-nowrap text-foreground/85 transition-colors hover:border-primary/60 hover:text-foreground disabled:opacity-50"
                                        onClick={() => sendMessage(q)}
                                        disabled={loading}
                                    >
                                        {q}
                                    </button>
                                ))}
                            </div>
                        )}

                        {activeTab === 'actions' && token && (
                            <>
                                <div className="flex max-h-28 flex-wrap gap-1.5 overflow-y-auto">
                                    {actionButtons.map((a, idx) => (
                                        <button
                                            key={idx}
                                            className="cursor-pointer rounded-full border px-2.5 py-1 text-xs whitespace-nowrap text-foreground/85 transition-colors hover:border-primary/60 hover:text-foreground disabled:opacity-50"
                                            disabled={loading}
                                            onClick={() => {
                                                if (a.send) {
                                                    sendMessage(a.send);
                                                } else {
                                                    setInput(a.fill);
                                                    setTimeout(() => {
                                                        const inp = inputRef.current;
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
                                            {a.fill && <span className="text-primary"> →</span>}
                                        </button>
                                    ))}
                                </div>
                                <p className="mt-1.5 text-[10px] text-muted-foreground">
                                    → fills input &nbsp;|&nbsp; no arrow = sends directly
                                </p>
                            </>
                        )}
                    </div>

                    {/* Listening status bar */}
                    {isListening && (
                        <div className="flex items-center gap-2 bg-destructive/15 px-4 py-1.5 text-xs font-medium text-destructive">
                            <span className="size-2 animate-ping rounded-full bg-destructive" />
                            Listening... speak now
                        </div>
                    )}

                    {/* Input area */}
                    <div className="flex items-center gap-2 border-t p-3">
                        <input
                            ref={inputRef}
                            type="text"
                            className="h-10 min-w-0 flex-1 rounded-full border border-input bg-background px-4 text-sm outline-none placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:opacity-60 dark:bg-input/30"
                            placeholder={isListening ? "Listening..." : "Ask a question..."}
                            value={input}
                            onChange={(e) => setInput(e.target.value)}
                            onKeyDown={handleKeyDown}
                            disabled={loading || isListening}
                        />

                        {/* Microphone button */}
                        {speechSupported && (
                            <Button
                                variant={isListening ? "destructive" : "ghost"}
                                size="icon"
                                className={cn("rounded-full", isListening && "animate-pulse")}
                                onClick={isListening ? stopListening : startListening}
                                disabled={loading}
                                aria-label={isListening ? "Stop listening" : "Start voice input"}
                                title={isListening ? "Click to stop" : "Voice input"}
                            >
                                {isListening ? <MicOff /> : <Mic />}
                            </Button>
                        )}

                        {/* Send button */}
                        <Button
                            size="icon"
                            className="rounded-full"
                            onClick={() => input.trim() && sendMessage(input)}
                            disabled={loading || !input.trim() || isListening}
                            aria-label="Send message"
                        >
                            <SendHorizontal />
                        </Button>
                    </div>
                </div>
            )}
        </>
    );
};

export default Chatbot;
