use std::{env, fs, io::Write, path::Path, process::{Command, Stdio}};

fn main() {
    let args: Vec<String> = env::args().collect();
    let request = fs::read(&args[2]).expect("read request");
    let mut child = Command::new(&args[1])
        .args(["invoke", "Standard.SheetMetal.ElectronicsEnclosure", "--request", "-", "--out", &args[3]])
        .stdin(Stdio::piped()).stdout(Stdio::piped()).spawn().expect("start Forge Host");
    child.stdin.as_mut().unwrap().write_all(&request).expect("write request");
    let output = child.wait_with_output().expect("wait for Forge Host");
    let response = String::from_utf8(output.stdout).expect("UTF-8 response");
    assert!(output.status.success() && response.contains("\"success\": true"), "{}", response);
    for name in ["part.step", "part.flat.step", "part.flat.svg"] {
        assert!(Path::new(&args[3]).join(name).is_file(), "missing artifact {}", name);
    }
    print!("{response}");
}
