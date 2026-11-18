import { CommonModule } from '@angular/common';
import {
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  ViewChild,
  inject,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  ChatMessage,
  CopilotChatRequest,
  CopilotService,
  ExecutedTool,
} from '../../services/copilot.service';

@Component({
  selector: 'app-copilot-widget',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './copilot-widget.html',
  styleUrl: './copilot-widget.scss',
})
export class CopilotWidgetComponent {
  @ViewChild('messagesContainer')
  private messagesContainer?: ElementRef<HTMLElement>;

  isOpen = false;
  isLoading = false;
  userInput = '';

  readonly quickPrompts: string[] = [
    'What is my spending summary?',
    'List my recent expenses',
    'What categories are available?',
    'Add $18.50 for Coffee under Food',
  ];

  messages: ChatMessage[] = [];
  expandedTools: Record<string, boolean> = {};

  private readonly copilotService = inject(CopilotService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdr = inject(ChangeDetectorRef);

  toggleOpen(): void {
    this.isOpen = !this.isOpen;
    if (this.isOpen) {
      this.scrollToBottom();
    }
  }

  close(): void {
    this.isOpen = false;
  }

  clearChat(): void {
    if (!this.isLoading) {
      this.messages = [];
    }
  }

  sendQuickPrompt(prompt: string): void {
    this.userInput = prompt;
    this.sendMessage();
  }

  toggleTool(toolKey: string): void {
    this.expandedTools[toolKey] = !this.expandedTools[toolKey];
  }

  sendMessage(): void {
    const text = this.userInput.trim();
    if (!text || this.isLoading) {
      return;
    }

    const userMsg: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content: text,
      timestamp: new Date(),
    };

    this.messages.push(userMsg);
    this.userInput = '';
    this.isLoading = true;
    this.scrollToBottom();

    // Map last 6 messages to history
    const history = this.messages
      .filter((m) => m.role !== 'error')
      .slice(-6)
      .map((m) => ({
        role: m.role === 'assistant' ? 'model' : 'user',
        content: m.content,
      }));

    const request: CopilotChatRequest = {
      message: text,
      history: history.slice(0, -1), // exclude the one just added
    };

    this.copilotService
      .chat(request)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => {
          this.isLoading = false;
          this.cdr.detectChanges();
          this.scrollToBottom();
        }),
      )
      .subscribe({
        next: (res) => {
          this.messages.push({
            id: crypto.randomUUID(),
            role: res.success ? 'assistant' : 'error',
            content: res.reply,
            timestamp: new Date(),
            toolsExecuted: res.toolsExecuted,
          });
          this.cdr.detectChanges();
        },
        error: (err) => {
          this.messages.push({
            id: crypto.randomUUID(),
            role: 'error',
            content:
              err?.error?.detail ||
              err?.error?.reply ||
              'Failed to communicate with Copilot. Please verify API settings.',
            timestamp: new Date(),
          });
          this.cdr.detectChanges();
        },
      });
  }

  formatJson(raw: string): string {
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const el = this.messagesContainer?.nativeElement;
      if (el) {
        el.scrollTop = el.scrollHeight;
      }
    }, 50);
  }
}
