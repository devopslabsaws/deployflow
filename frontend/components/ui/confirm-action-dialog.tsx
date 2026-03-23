"use client";

import { useEffect, useState } from "react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

interface ConfirmActionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: string;
  confirmLabel?: string;
  cancelLabel?: string;
  confirmVariant?: "default" | "destructive" | "outline" | "secondary" | "ghost" | "link";
  requireText?: string;
  requireTextLabel?: string;
  isConfirming?: boolean;
  onConfirm: () => Promise<void> | void;
}

export function ConfirmActionDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  confirmVariant = "destructive",
  requireText,
  requireTextLabel,
  isConfirming = false,
  onConfirm,
}: ConfirmActionDialogProps) {
  const [typedText, setTypedText] = useState("");

  useEffect(() => {
    if (!open) {
      setTypedText("");
    }
  }, [open]);

  const requiresMatch = Boolean(requireText);
  const isMatch = !requiresMatch || typedText.trim() === requireText;

  const handleConfirm = async () => {
    try {
      await onConfirm();
      onOpenChange(false);
    } catch {
      // Keep dialog open when action fails.
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        {requiresMatch && (
          <div className="space-y-2">
            <Label htmlFor="confirm-action-input">{requireTextLabel ?? `Type \"${requireText}\" to confirm`}</Label>
            <Input
              id="confirm-action-input"
              value={typedText}
              onChange={(e) => setTypedText(e.target.value)}
              placeholder={requireText}
              autoComplete="off"
            />
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isConfirming}>
            {cancelLabel}
          </Button>
          <Button
            variant={confirmVariant}
            onClick={handleConfirm}
            disabled={isConfirming || !isMatch}
          >
            {isConfirming ? "Please wait..." : confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
