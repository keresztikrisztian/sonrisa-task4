import { Component, OnInit, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { IdentityService } from './core/identity.service';

@Component({
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './shell.html',
})
export class App implements OnInit {
  protected readonly identity = inject(IdentityService);

  ngOnInit(): void { this.identity.loadIdentities(); }

  protected selectIdentity(event: Event): void {
    this.identity.select((event.target as HTMLSelectElement).value);
  }
}
