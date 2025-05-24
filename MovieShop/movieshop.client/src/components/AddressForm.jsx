import { useEffect, useState } from "react";

const AddressForm = ({ title, initialAddress, onSave }) => {
    const [address, setAddress] = useState(initialAddress || {
        street: "",
        city: "",
        zip: "",
    });

    const [savedMessage, setSavedMessage] = useState("");

    useEffect(() => {
        if (initialAddress) {
            setAddress(initialAddress);
        }
    }, [initialAddress]);

    const handleChange = (e) => {
        const { name, value } = e.target;
        setAddress((prev) => ({ ...prev, [name]: value }));
    };

    const handleSave = () => {
        if (!address.street || !address.city || !address.zip) {
            alert("Every field must be filled!");
            return;
        }

        if (!/^\d{4}$/.test(address.zip)) {
            alert("Zip must be 4 numbers");
            return;
        }

        onSave(address);
        setSavedMessage("Saved");
        setTimeout(() => setSavedMessage(""), 2000);
    };


    return (
        <div className="mb-4 p-3 border rounded">
            <h5>{title}</h5>
            <div className="mb-2">
                <label>Street, house number:</label>
                <input
                    name="street"
                    value={address.street}
                    onChange={handleChange}
                    className="form-control"
                />
            </div>
            <div className="mb-2">
                <label>City:</label>
                <input
                    name="city"
                    value={address.city}
                    onChange={handleChange}
                    className="form-control"
                />
            </div>
            <div className="mb-2">
                <label>Zip:</label>
                <input
                    name="zip"
                    value={address.zip}
                    onChange={handleChange}
                    className="form-control"
                />
            </div>
            <button className="btn btn-primary" onClick={handleSave}>
                Save
            </button>
            {savedMessage && <div className="mt-2 text-success">{savedMessage}</div>}
        </div>
    );
};

export default AddressForm;
