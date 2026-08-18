export function AuthDivider() {
  return (
    <div className="flex items-center gap-3" aria-hidden="true">
      <span className="bg-border h-px flex-1" />
      <span className="text-muted-foreground text-xs">or</span>
      <span className="bg-border h-px flex-1" />
    </div>
  );
}
