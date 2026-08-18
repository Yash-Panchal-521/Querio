"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Trash2 } from "lucide-react";
import { toApiMessage, toFieldErrors } from "@/lib/api/api-messages";
import { deleteOrganization, renameOrganization } from "@/lib/api/tenants";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, Page, PageHeader } from "@/components/app/page-shell";
import { Field } from "@/components/ui/field";
import { OrganizationGate } from "@/components/app/organization-gate";
import type { Organization } from "@/lib/api/me";
import { useOrganizations } from "@/lib/auth/use-organizations";
import { useSession } from "@/lib/auth/session-context";
import { useToast } from "@/components/ui/toast";

export function OrganizationSettings() {
  return (
    <OrganizationGate>
      <Settings />
    </OrganizationGate>
  );
}

function Settings() {
  const { active } = useOrganizations();

  if (!active) {
    return null;
  }

  // The API refuses these for anyone but an owner. Hiding them as well means a member is not
  // shown a control that only ever produces a refusal.
  if (active.role !== "Owner") {
    return (
      <Page width="narrow">
        <Alert tone="info" title="Owners only">
          Only an owner can rename or delete {active.name}.
        </Alert>
        <Link href={`/orgs/${active.id}`} className="text-primary text-sm hover:underline">
          Back to {active.name}
        </Link>
      </Page>
    );
  }

  return (
    <Page>
      <PageHeader
        eyebrow={active.name}
        title="Settings"
        description="Rename this organization, or remove it and everything in it."
      />

      <RenameCard organization={active} />
      <DeleteCard organization={active} />
    </Page>
  );
}

function RenameCard({ organization }: { organization: Organization }) {
  const { refresh } = useSession();
  const { showToast } = useToast();

  const [name, setName] = useState(organization.name);
  const [fieldError, setFieldError] = useState<string | undefined>(undefined);
  const [pending, setPending] = useState(false);

  const unchanged = name.trim() === organization.name;

  return (
    <Card
      title="Name"
      description="Your team sees this everywhere. The web address is left alone, so links already shared keep working."
    >
      <form
        noValidate
        className="flex flex-col gap-4"
        onSubmit={(event) => {
          event.preventDefault();
          setFieldError(undefined);
          setPending(true);

          void renameOrganization(organization.id, name)
            .then(async () => {
              await refresh();
              showToast("Organization renamed.", "success");
            })
            .catch((caught: unknown) => {
              setFieldError(toFieldErrors(caught).name);
              showToast(toApiMessage(caught), "error");
            })
            .finally(() => setPending(false));
        }}
      >
        <Field
          label="Organization name"
          name="name"
          required
          hint={`Web address: ${organization.slug}`}
          error={fieldError}
          value={name}
          onChange={(event) => setName(event.target.value)}
        />

        <Button type="submit" loading={pending} disabled={unchanged} className="self-start">
          Save changes
        </Button>
      </form>
    </Card>
  );
}

function DeleteCard({ organization }: { organization: Organization }) {
  const { refresh } = useSession();
  const { showToast } = useToast();
  const router = useRouter();

  const [confirmation, setConfirmation] = useState("");
  const [pending, setPending] = useState(false);

  // Typing the name is the friction. A dialog with a red button is dismissed on reflex; this
  // cannot be completed without reading what is about to be destroyed.
  const confirmed = confirmation.trim() === organization.name;

  return (
    <Card
      tone="danger"
      title="Delete this organization"
      description="Every member loses access, and all documents, invitations and conversations are removed. This cannot be undone."
    >
      <form
        noValidate
        className="flex flex-col gap-4"
        onSubmit={(event) => {
          event.preventDefault();

          if (!confirmed) {
            return;
          }

          setPending(true);

          void deleteOrganization(organization.id)
            .then(async () => {
              await refresh();
              showToast(`${organization.name} was deleted.`, "success");
              router.replace("/orgs");
            })
            .catch((caught: unknown) => {
              showToast(toApiMessage(caught), "error");
              setPending(false);
            });
        }}
      >
        <Field
          label={`Type "${organization.name}" to confirm`}
          name="confirmation"
          autoComplete="off"
          placeholder={organization.name}
          value={confirmation}
          onChange={(event) => setConfirmation(event.target.value)}
        />

        <Button
          type="submit"
          variant="destructive"
          loading={pending}
          disabled={!confirmed}
          className="self-start"
        >
          <Trash2 />
          Delete organization
        </Button>
      </form>
    </Card>
  );
}
