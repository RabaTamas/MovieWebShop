import { useState } from 'react';
import './Chatbot.css';
import API_BASE_URL from '../config/api';
import { useAuth } from '../contexts/AuthContext';

const Chatbot = () => {
    const { token } = useAuth(); // Get auth token
    const [isOpen, setIsOpen] = useState(false);
    const [messages, setMessages] = useState([
        { text: "👋 Hi! How can I help you?", sender: "bot" }
    ]);

    const [input, setInput] = useState("");
    const [loading, setLoading] = useState(false);

    const faqButtons = [
        "What payment methods do you accept?",
        "How do I watch my movies?",
        "Can I get a refund?",
        "How do I contact you?"
    ];

    // Generate or retrieve session ID (doesn't need to be state)
    const sessionId = (() => {
        let id = localStorage.getItem('chatSessionId');
        if (!id) {
            id = `session_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
            localStorage.setItem('chatSessionId', id);
        }
        return id;
    })();

    const sendMessage = async (text) => {
        const userMessage = { text, sender: "user" };
        setMessages(prev => [...prev, userMessage]);
        setInput("");
        setLoading(true);

        try {
            const headers = {
                'Content-Type': 'application/json'
            };

            // Add authorization header if user is logged in
            if (token) {
                headers['Authorization'] = `Bearer ${token}`;
            }

            const response = await fetch(`${API_BASE_URL}/api/Chat/ask`, {
                method: 'POST',
                headers: headers,
                body: JSON.stringify({
                    question: text,
                    sessionId: sessionId
                })
            });

            if (!response.ok) {
                throw new Error('Network response was not ok');
            }

            const data = await response.json();
            const botMessage = {
                text: data.answer,
                sender: "bot",
                source: data.source
            };
            setMessages(prev => [...prev, botMessage]);
        } catch (err) {
            console.error('Chat error:', err);
            setMessages(prev => [...prev, {
                text: "😔 An error occurred. Please try again or email us: support@movieshop.com",
                sender: "bot"
            }]);
        } finally {
            setLoading(false);
        }
    };

    const handleKeyPress = (e) => {
        if (e.key === 'Enter' && input.trim() && !loading) {
            sendMessage(input);
        }
    };

    return (
        <>
            {/* Floating bubble button */}
            {!isOpen && (
                <button
                    className="chatbot-bubble"
                    onClick={() => setIsOpen(true)}
                    aria-label="Open chat"
                >
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
                        <button
                            onClick={() => setIsOpen(false)}
                            aria-label="Close chat"
                        >
                            ✕
                        </button>
                    </div>

                    {/* Messages */}
                    <div className="chatbot-messages">
                        {messages.map((msg, idx) => (
                            <div key={idx} className={`message ${msg.sender}`}>
                                <div className="message-bubble">
                                    {msg.text}
                                    {msg.source && (
                                        <small className="message-source">
                                            {msg.source === 'FAQ' ? '📚 FAQ' : '🤖 AI'}
                                        </small>
                                    )}
                                </div>
                            </div>
                        ))}
                        {loading && (
                            <div className="message bot">
                                <div className="message-bubble">
                                    <div className="typing-indicator">
                                        <span></span>
                                        <span></span>
                                        <span></span>
                                    </div>
                                </div>
                            </div>
                        )}
                    </div>

                    {/* FAQ quick buttons */}
                    <div className="chatbot-faq">
                        <small className="text-muted mb-2 d-block">Quick questions:</small>
                        {faqButtons.map((q, idx) => (
                            <button
                                key={idx}
                                className="btn btn-sm btn-outline-primary"
                                onClick={() => sendMessage(q)}
                                disabled={loading}
                            >
                                {q}
                            </button>
                        ))}
                    </div>

                    {/* Input area */}
                    <div className="chatbot-input">
                        <input
                            type="text"
                            placeholder="Ask a question..."
                            value={input}
                            onChange={(e) => setInput(e.target.value)}
                            onKeyPress={handleKeyPress}
                            disabled={loading}
                        />

                        <button
                            onClick={() => input.trim() && sendMessage(input)}
                            disabled={loading || !input.trim()}
                            aria-label="Send message"
                        >
                            ➤
                        </button>
                    </div>
                </div>
            )}
        </>
    );
};

export default Chatbot;