import { useEffect, useState } from "react";
import { toast } from "sonner";
import { CheckCircle2, MapPin } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

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
            toast.error("Every field must be filled!");
            return;
        }

        if (!/^\d{4}$/.test(address.zip)) {
            toast.error("Zip must be 4 numbers");
            return;
        }

        onSave(address);
        setSavedMessage("Saved");
        setTimeout(() => setSavedMessage(""), 2000);
    };

    return (
        <Card>
            <CardHeader>
                <CardTitle className="flex items-center gap-2">
                    <MapPin className="size-4 text-primary" />
                    {title}
                </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
                <div className="space-y-2">
                    <Label htmlFor="address-street">Street, house number</Label>
                    <Input id="address-street" name="street" value={address.street} onChange={handleChange} />
                </div>
                <div className="grid grid-cols-[1fr_110px] gap-3">
                    <div className="space-y-2">
                        <Label htmlFor="address-city">City</Label>
                        <Input id="address-city" name="city" value={address.city} onChange={handleChange} />
                    </div>
                    <div className="space-y-2">
                        <Label htmlFor="address-zip">Zip</Label>
                        <Input id="address-zip" name="zip" inputMode="numeric" value={address.zip} onChange={handleChange} />
                    </div>
                </div>
                <div className="flex items-center gap-3">
                    <Button variant="secondary" onClick={handleSave}>
                        Save
                    </Button>
                    {savedMessage && (
                        <span className="flex items-center gap-1.5 text-sm text-success">
                            <CheckCircle2 className="size-4" /> {savedMessage}
                        </span>
                    )}
                </div>
            </CardContent>
        </Card>
    );
};

export default AddressForm;
