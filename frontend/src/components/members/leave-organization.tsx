"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { LogOut } from "lucide-react";
import { toApiMessage } from "@/lib/api/api-messages";
import type { Organization } from "@/lib/api/me";
import { leaveOrganization } from "@/lib/api/members";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/app/page-shell";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { useSession } from "@/lib/auth/session-context";
import { useToast } from "@/components/ui/toast";

export function LeaveOrganization({ organization }: { organization: Organization }) {
  const { refresh } = useSession();
  const { showToast } = useToast();
  const router = useRouter();

  const [open, setOpen] = useState(false);
  const [pending, setPending] = useState(false);

  async function leave() {
    setPending(true);

    try {
      await leaveOrganization(organization.id);
      await refresh();

      showToast(`You have left ${organization.name}.`, "success");

      // Back to the entry point, which decides where they can still go.
      router.replace("/orgs");
    } catch (caught) {
      // The last owner is refused here, and the message explains what to do first.
      showToast(toApiMessage(caught), "error");
      setPending(false);
      setOpen(false);
    }
  }

  return (
    <Card
      title="Leave this organization"
      description="You will lose access to its documents. An owner can invite you back."
      tone="danger"
      actions={
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild>
            <Button variant="secondary" size="sm">
              <LogOut />
              Leave
            </Button>
          </DialogTrigger>

          {/* A dialog rather than an inline confirm, because leaving is irreversible from
              the leaver's side and deserves to interrupt. */}
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Leave {organization.name}?</DialogTitle>
              <DialogDescription>
                You will immediately lose access to every document in this organization. An owner
                can invite you back afterwards.
              </DialogDescription>
            </DialogHeader>

            <DialogFooter>
              <Button variant="secondary" onClick={() => setOpen(false)} disabled={pending}>
                Cancel
              </Button>
              <Button variant="destructive" loading={pending} onClick={() => void leave()}>
                Leave organization
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      }
    />
  );
}
