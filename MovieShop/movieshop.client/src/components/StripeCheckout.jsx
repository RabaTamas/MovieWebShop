import { useState } from 'react';
import { CardElement, useStripe, useElements } from '@stripe/react-stripe-js';
import { useTheme } from '../contexts/ThemeContext';
import { formatPrice } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Spinner } from '@/components/ui/spinner';

const StripeCheckout = ({ amount, onSuccess, onError }) => {
    const stripe = useStripe();
    const elements = useElements();
    const [processing, setProcessing] = useState(false);
    const { theme } = useTheme();

    const handleSubmit = async (e) => {
        e.preventDefault();

        if (!stripe || !elements) return;

        setProcessing(true);

        try {
            const cardElement = elements.getElement(CardElement);

            const { error, paymentMethod } = await stripe.createPaymentMethod({
                type: 'card',
                card: cardElement,
            });

            if (error) {
                onError(error.message);
                setProcessing(false);
                return;
            }

            onSuccess(paymentMethod.id);
        } catch (err) {
            onError(err.message);
        } finally {
            setProcessing(false);
        }
    };

    // A Stripe kártyamező iframe-ben fut, ezért a témaszíneket explicit kell átadni
    const isDark = theme === 'dark';

    return (
        <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
                <Label>Card Details</Label>
                <div className="rounded-md border border-input bg-background px-3 py-3 shadow-xs dark:bg-input/30">
                    <CardElement options={{
                        style: {
                            base: {
                                fontSize: '16px',
                                color: isDark ? '#f4f4f5' : '#18181b',
                                iconColor: isDark ? '#f0b43c' : '#b7791f',
                                '::placeholder': { color: isDark ? '#71717a' : '#a1a1aa' },
                            },
                            invalid: { color: '#ef4444' },
                        },
                    }} />
                </div>
            </div>
            <Button type="submit" size="lg" className="w-full" disabled={!stripe || processing}>
                {processing && <Spinner />}
                {processing ? 'Processing...' : `Pay ${formatPrice(amount)}`}
            </Button>
        </form>
    );
};

export default StripeCheckout;
