import { useState } from 'react';
import { CardElement, useStripe, useElements } from '@stripe/react-stripe-js';

const StripeCheckout = ({ amount, onSuccess, onError }) => {
    const stripe = useStripe();
    const elements = useElements();
    const [processing, setProcessing] = useState(false);

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

    return (
        <form onSubmit={handleSubmit}>
            <div className="mb-3">
                <label className="form-label">Card Details</label>
                <div className="p-3 border rounded">
                    <CardElement options={{
                        style: {
                            base: {
                                fontSize: '16px',
                                color: '#424770',
                                '::placeholder': { color: '#aab7c4' },
                            },
                        },
                    }} />
                </div>
            </div>
            <button
                type="submit"
                className="btn btn-primary w-100"
                disabled={!stripe || processing}
            >
                {processing ? 'Processing...' : `Pay ${amount} Ft`}
            </button>
        </form>
    );
};

export default StripeCheckout;