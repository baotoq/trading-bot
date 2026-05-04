mod cli;
mod config;

use clap::Parser;

fn main() {
    let args = cli::Cli::parse();
    println!("{args:?}");
}
