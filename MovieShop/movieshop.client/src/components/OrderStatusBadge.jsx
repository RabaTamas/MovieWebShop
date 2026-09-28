import { Badge } from "@/components/ui/badge";

// A backend az OrderStatus enumot névként vagy számként is küldheti
const STATUS_NAMES = ["Pending", "Processing", "Shipped", "Delivered", "Completed", "Cancelled"];

const toneFor = (name) => {
    switch ((name || "").toLowerCase()) {
        case "completed":
        case "delivered":
            return "success";
        case "cancelled":
        case "canceled":
        case "failed":
            return "danger";
        case "pending":
            return "warning";
        default:
            return "muted";
    }
};

const OrderStatusBadge = ({ status, className }) => {
    const name = typeof status === "number" ? STATUS_NAMES[status] ?? String(status) : status;
    return (
        <Badge variant={toneFor(name)} className={className}>
            {name}
        </Badge>
    );
};

export default OrderStatusBadge;
