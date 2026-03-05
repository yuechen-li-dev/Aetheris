import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';

import { cn } from '../../lib/utils';

const buttonVariants = cva(
  'inline-flex items-center justify-center whitespace-nowrap rounded-[var(--ui-radius)] border-[var(--ui-stroke)] text-[13px] font-semibold transition-colors focus-visible:outline-none focus-visible:ring-[var(--ui-focus-ring)] focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50',
  {
    variants: {
      variant: {
        default: 'border-primary bg-primary text-primary-foreground hover:opacity-90',
        secondary: 'border-border bg-muted text-foreground hover:bg-muted/80',
        ghost: 'border-transparent bg-transparent hover:bg-muted hover:text-foreground',
        outline: 'border-input bg-background text-foreground hover:bg-muted',
      },
      size: {
        default: 'h-[34px] px-3',
        sm: 'h-[32px] px-3 text-[13px]',
        lg: 'h-[36px] px-6 text-[14px]',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'default',
    },
  },
);

function Button({ className, variant, size, ...props }: React.ComponentProps<'button'> & VariantProps<typeof buttonVariants>) {
  return <button className={cn(buttonVariants({ variant, size, className }))} {...props} />;
}

export { Button, buttonVariants };
