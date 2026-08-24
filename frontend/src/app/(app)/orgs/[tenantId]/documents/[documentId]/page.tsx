import type { Metadata } from "next";
import { DocumentScreen } from "./document-screen";

export const metadata: Metadata = { title: "Document" };

/**
 * `params` is a promise in this version of Next, so the id is awaited here and handed to the
 * client component rather than read from a hook.
 */
export default async function DocumentPage({
  params,
}: {
  params: Promise<{ documentId: string }>;
}) {
  const { documentId } = await params;

  return <DocumentScreen documentId={documentId} />;
}
