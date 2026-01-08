import { useState, useEffect } from "react";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "sonner";

interface EditAnswerDialogProps {
  answer: { id: number; answerText: string; weight: number } | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: () => void;
}

const EditAnswerDialog = ({
  answer,
  open,
  onOpenChange,
  onSave,
}: EditAnswerDialogProps) => {
  const [answerText, setAnswerText] = useState("");
  const [weight, setWeight] = useState(1);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (answer) {
      setAnswerText(answer.answerText);
      setWeight(answer.weight);
    }
  }, [answer]);

  const handleSave = async () => {
    if (!answer) return;

    if (!answerText.trim()) {
      toast.error("Текст ответа обязателен");
      return;
    }

    setLoading(true);
    try {
      const response = await fetch(`/api/Survey/answers/${answer.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          answerText: answerText.trim(),
          weight: weight,
        }),
      });

      if (response.ok) {
        toast.success("Ответ обновлен");
        onSave();
        onOpenChange(false);
      } else {
        let errorMessage = "Не удалось обновить ответ";
        try {
          const error = await response.json();
          errorMessage = error.error || error.message || errorMessage;
        } catch {
          errorMessage = `Ошибка ${response.status}: ${response.statusText}`;
        }
        toast.error(errorMessage);
      }
    } catch (error) {
      console.error("Failed to update answer:", error);
      toast.error("Ошибка при обновлении ответа");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Редактировать ответ</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label htmlFor="answerText">Текст ответа *</Label>
            <Input
              id="answerText"
              value={answerText}
              onChange={(e) => setAnswerText(e.target.value)}
              placeholder="Введите текст ответа"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="weight">Вес</Label>
            <Input
              id="weight"
              type="number"
              value={weight}
              onChange={(e) => setWeight(parseInt(e.target.value) || 1)}
              min="1"
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Отмена
          </Button>
          <Button onClick={handleSave} disabled={loading}>
            {loading ? "Сохранение..." : "Сохранить"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default EditAnswerDialog;

