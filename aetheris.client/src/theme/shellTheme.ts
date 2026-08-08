export const shellTheme = {
	color: {
		background: "#d8d3c5",
		panel: "#e5e0d3",
		elevated: "#eee9dc",
		textPrimary: "#292824",
		textSecondary: "#656158",
		border: "#8e897d",
		accent: "#554f43",
		warning: "#8f642c",
		error: "#8a3f35",
		success: "#4f6652",
		selection: "#b5a47a",
		disabled: "#a6a094",
	},
	spacing: { xs: 4, sm: 8, md: 12, lg: 18, xl: 24 },
	typography: { family: "Inter, sans-serif", body: 13, small: 11, title: 12 },
	radius: 1,
} as const;

export function shellThemeCssVariables(): Record<string, string> {
	return {
		"--shell-background": shellTheme.color.background,
		"--shell-panel": shellTheme.color.panel,
		"--shell-elevated": shellTheme.color.elevated,
		"--shell-text-primary": shellTheme.color.textPrimary,
		"--shell-text-secondary": shellTheme.color.textSecondary,
		"--shell-border": shellTheme.color.border,
		"--shell-accent": shellTheme.color.accent,
		"--shell-warning": shellTheme.color.warning,
		"--shell-error": shellTheme.color.error,
		"--shell-success": shellTheme.color.success,
		"--shell-selection": shellTheme.color.selection,
		"--shell-disabled": shellTheme.color.disabled,
		"--shell-radius": `${shellTheme.radius}px`,
	};
}
