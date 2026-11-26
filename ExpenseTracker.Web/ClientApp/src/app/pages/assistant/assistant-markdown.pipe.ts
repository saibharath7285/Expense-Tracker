import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'assistantMarkdown',
  standalone: true,
})
export class AssistantMarkdownPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) {
      return '';
    }

    const lines = value.replace(/\r\n?/g, '\n').split('\n');
    const html: string[] = [];
    let insideCodeBlock = false;

    for (const rawLine of lines) {
      const line = rawLine.trimEnd();

      if (/^\s*```/.test(line)) {
        insideCodeBlock = !insideCodeBlock;
        continue;
      }

      if (insideCodeBlock) {
        html.push(`<pre class="assistant-code">${this.escapeHtml(line)}</pre>`);
        continue;
      }

      if (!line.trim()) {
        html.push('<div class="assistant-spacer" aria-hidden="true"></div>');
        continue;
      }

      const heading = line.match(/^\s*#{1,6}\s+(.+)$/);
      if (heading) {
        html.push(
          `<div class="assistant-heading">${this.formatInline(heading[1])}</div>`,
        );
        continue;
      }

      const orderedItem = line.match(/^\s*(\d+)[.)]\s+(.+)$/);
      if (orderedItem) {
        html.push(
          `<div class="assistant-list-item"><span class="assistant-list-marker">${orderedItem[1]}.</span>${this.formatInline(orderedItem[2])}</div>`,
        );
        continue;
      }

      const unorderedItem = line.match(/^\s*[-*+]\s+(.+)$/);
      if (unorderedItem) {
        html.push(
          `<div class="assistant-list-item"><span class="assistant-list-marker">•</span>${this.formatInline(unorderedItem[1])}</div>`,
        );
        continue;
      }

      html.push(`<div>${this.formatInline(line)}</div>`);
    }

    return html.join('');
  }

  private formatInline(value: string): string {
    let formatted = this.escapeHtml(value);

    formatted = formatted.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
    formatted = formatted.replace(/__(.+?)__/g, '<strong>$1</strong>');
    formatted = formatted.replace(/`([^`]+)`/g, '<code>$1</code>');
    formatted = formatted.replace(/\*([^*]+)\*/g, '<em>$1</em>');

    return formatted;
  }

  private escapeHtml(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }
}
