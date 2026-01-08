import { useState } from "react";
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

interface CreateAnswerDialogProps {
  questionId: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: () => void;
}

const CreateAnswerDialog = ({
  questionId,
  open,
  onOpenChange,
  onSave,
}: CreateAnswerDialogProps) => {
  const [answerText, setAnswerText] = useState("");
  const [weight, setWeight] = useState(1);
  const [loading, setLoading] = useState(false);

  const handleSave = async () => {
    if (!answerText.trim()) {
      toast.error("Текст ответа обязателен");
      return;
    }

    setLoading(true);
    try {
      const response = await fetch("/api/Survey/answers", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          questionId: questionId,
          answerText: answerText.trim(),
          weight: weight,
        }),
      });

      if (response.ok) {
        toast.success("Ответ создан");
        setAnswerText("");
        setWeight(1);
        onSave();
        onOpenChange(false);
      } else {
        let errorMessage = "Не удалось создать ответ";
        try {
          const error = await response.json();
          errorMessage = error.error || error.message || errorMessage;
        } catch {
          errorMessage = `Ошибка ${response.status}: ${response.statusText}`;
        }
        toast.error(errorMessage);
      }
    } catch (error) {
      console.error("Failed to create answer:", error);
      toast.error("Ошибка при создании ответа");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Добавить ответ</DialogTitle>
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
            {loading ? "Создание..." : "Создать"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default CreateAnswerDialog;

