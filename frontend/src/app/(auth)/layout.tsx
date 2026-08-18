export default function AuthLayout({ children }: LayoutProps<"/">) {
  return <div className="flex min-h-full flex-1 flex-col">{children}</div>;
}
