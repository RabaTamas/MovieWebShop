import { toast } from "sonner";
import { motion } from "motion/react";
import { Bell, BellOff, BellRing, Send, Share, ShieldAlert } from "lucide-react";

import usePushNotifications from "@/hooks/usePushNotifications";
import { cn } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";

/** Profil oldali kártya: push értesítések be- és kikapcsolása, próbaértesítés. */
export default function NotificationSettings() {
    const push = usePushNotifications();

    const enable = async () => {
        const { ok, error } = await push.subscribe();
        if (ok) toast.success("Notifications enabled", { description: "We'll let you know when a new movie arrives." });
        else toast.error("Could not enable notifications", { description: error });
    };

    const disable = async () => {
        const { ok, error } = await push.unsubscribe();
        if (ok) toast("Notifications turned off");
        else toast.error("Could not turn off notifications", { description: error });
    };

    const test = async () => {
        const { ok, result, error } = await push.sendTest();
        if (ok) toast.success("Test notification sent", { description: `Delivered to ${result.sent} device${result.sent !== 1 ? "s" : ""}.` });
        else toast.error("No notification was delivered", { description: error ?? "No active subscription for this account." });
    };

    let body;
    if (!push.supported) {
        body = <p className="text-sm text-muted-foreground">Your browser doesn't support push notifications.</p>;
    } else if (push.needsInstallFirst) {
        body = (
            <p className="flex items-start gap-2 text-sm text-muted-foreground">
                <Share className="mt-0.5 size-4 shrink-0 text-primary" />
                On iPhone and iPad, add MovieWebShop to your Home Screen first (Share → Add to Home Screen), then enable notifications from the installed app.
            </p>
        );
    } else if (!push.swReady) {
        body = <p className="text-sm text-muted-foreground">Notifications are available in the installed / production build of the app.</p>;
    } else if (push.permission === "denied") {
        body = (
            <p className="flex items-start gap-2 text-sm text-muted-foreground">
                <ShieldAlert className="mt-0.5 size-4 shrink-0 text-destructive" />
                Notifications are blocked for this site. Allow them in your browser's site settings, then reload the page.
            </p>
        );
    } else if (push.subscribed) {
        body = (
            <div className="flex flex-wrap gap-2">
                <Button variant="outline" onClick={test} disabled={push.busy}>
                    {push.busy ? <Spinner /> : <Send />}
                    Send test notification
                </Button>
                <Button variant="ghost" className="text-muted-foreground" onClick={disable} disabled={push.busy}>
                    <BellOff />
                    Turn off
                </Button>
            </div>
        );
    } else {
        body = (
            <Button onClick={enable} disabled={push.busy}>
                {push.busy ? <Spinner /> : <BellRing />}
                Enable notifications
            </Button>
        );
    }

    return (
        <Card>
            <CardHeader>
                <CardTitle className="flex items-center gap-3">
                    <motion.span
                        className={cn(
                            "flex size-11 shrink-0 items-center justify-center rounded-full",
                            push.subscribed ? "bg-primary text-primary-foreground" : "bg-primary/15 text-primary"
                        )}
                        animate={push.subscribed ? { rotate: [0, -14, 12, -8, 6, 0] } : { rotate: 0 }}
                        transition={{ duration: 0.9, repeat: push.subscribed ? Infinity : 0, repeatDelay: 4 }}
                    >
                        <Bell className="size-5" />
                    </motion.span>
                    <span>
                        Notifications
                        <CardDescription className="mt-1 font-normal">
                            Get a notification on this device when a new movie arrives.
                        </CardDescription>
                    </span>
                </CardTitle>
                <CardAction>
                    {push.subscribed ? <Badge variant="success">On</Badge> : <Badge variant="muted">Off</Badge>}
                </CardAction>
            </CardHeader>
            <CardContent>{body}</CardContent>
        </Card>
    );
}
